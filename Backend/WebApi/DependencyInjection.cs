using System.Text;
using System.Threading.RateLimiting;
using Application.Abstractions.Notifications;
using Application.Common;
using Hangfire;
using Infrastructure.BackgroundJobs;
using Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WebApi.ExceptionHandling;
using WebApi.Hubs;
using WebApi.Middleware;
using WebApi.Options;
using WebApi.Realtime;
using WebApi.Security;

namespace WebApi;

/// <summary>
/// Registers and composes Web API layer concerns such as authentication, CORS, Swagger and middleware.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Web API services and presentation-layer adapters into the DI container.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddWebApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SignalROptions>()
            .Bind(configuration.GetSection(SignalROptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IRealtimeNotificationService, SignalRRealtimeNotificationService>();
        services.AddScoped<IRefreshTokenCookieService, RefreshTokenCookieService>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions["timestampUtc"] = DateTime.UtcNow;
            };
        });

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("WorkspaceOwner", policy => policy.RequireClaim("workspace_role", "Owner"));
            options.AddPolicy("WorkspaceAdmin", policy => policy.RequireClaim("workspace_role", "Owner", "Admin"));
            options.AddPolicy("WorkspaceEditor", policy => policy.RequireClaim("workspace_role", "Owner", "Admin", "Editor"));
        });

        services.AddCors(options =>
        {
            options.AddPolicy("Development", policy =>
            {
                if (corsOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(corsOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
                else
                {
                    policy
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });

            options.AddPolicy("Production", policy =>
            {
                policy
                    .WithOrigins(corsOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                    .WithHeaders("Authorization", "Content-Type", "X-Requested-With")
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromHours(1))
                    .WithExposedHeaders("X-Total-Count", "X-Page-Count");
            });
        });

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.GeneralWindowSeconds);
                limiterOptions.PermitLimit = rateLimitOptions.GeneralPermitLimit;
                limiterOptions.QueueLimit = 0;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            options.AddTokenBucketLimiter("upload", limiterOptions =>
            {
                limiterOptions.TokenLimit = rateLimitOptions.UploadTokenLimit;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitOptions.UploadReplenishmentSeconds);
                limiterOptions.TokensPerPeriod = rateLimitOptions.UploadTokensPerPeriod;
                limiterOptions.AutoReplenishment = true;
            });

            options.OnRejected = async (context, token) =>
            {
                var retryAfter = rateLimitOptions.RejectionRetryAfterSeconds;
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = "The request rate limit has been exceeded.",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problem.Extensions["retryAfterSeconds"] = retryAfter;

                await context.HttpContext.Response.WriteAsJsonAsync(problem, token);
            };
        });

        var signalROptions = configuration.GetSection(SignalROptions.SectionName).Get<SignalROptions>()
            ?? new SignalROptions();

        var signalRBuilder = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.KeepAliveInterval = TimeSpan.FromSeconds(signalROptions.KeepAliveSeconds);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(signalROptions.ClientTimeoutSeconds);
            options.MaximumReceiveMessageSize = signalROptions.MaximumReceiveMessageSize;
        });

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        if (!string.IsNullOrWhiteSpace(redisOptions?.ConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisOptions.ConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(signalROptions.ChannelPrefix);
            });
        }

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AutoPost API",
                Version = "v1",
                Description = "Multi-platform social publishing and automation API."
            });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Description = "Enter a valid bearer token."
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);

            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer"),
                    new List<string>()
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Configures the complete Web API middleware pipeline.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for fluent chaining.</returns>
    public static WebApplication UseWebApiPipeline(this WebApplication app)
    {
        var signalROptions = app.Services.GetRequiredService<IOptions<SignalROptions>>().Value;
        var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoPost API v1");
                options.RoutePrefix = "swagger";
            });
        }
        else
        {
            app.UseHsts();
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();

        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            await next();
        });

        app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");
        app.UseMiddleware<AuthRateLimitMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.UseHangfireDashboard(hangfireOptions.DashboardPath, new Hangfire.DashboardOptions
        {
            IsReadOnlyFunc = _ => !app.Environment.IsDevelopment()
        });

        using (var scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IRecurringJobRegistrar>().RegisterRecurringJobs();
        }

        app.MapHealthChecks("/health");
        app.MapControllers();
        app.MapHub<NotificationHub>(signalROptions.NotificationHubPath);

        return app;
    }
}
