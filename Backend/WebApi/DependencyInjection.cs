// WebApi/DependencyInjection.cs
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
using Microsoft.OpenApi.Models;
using WebApi.ExceptionHandling;
using WebApi.Hubs;
using WebApi.Middleware;
using WebApi.Options;
using WebApi.Realtime;
using WebApi.Security;

namespace WebApi;

/// <summary>
/// Registers and composes Web API presentation layer concerns such as Authentication, CORS, Rate Limiting, Swagger, and request pipelines.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Acts as the entry bootstrapping orchestrator for presentation concerns in a Clean Architecture topology.</para>
/// <para><b>Business &amp; Technical Justification:</b> Centralizes all endpoint mappings, SignalR configuration, authorization rules, and cross-cutting HTTP middlewares to enforce security and throughput requirements.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Serves as the bootstrapping glue between the host process (Program.cs) and layers below (Application and Infrastructure), translating HTTP requests to CQRS commands.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals presentation security boundaries, optimizes pipeline request performance (zero-allocation middleware), and prevents runtime initialization failures via strict configuration validation.</para>
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Web API services and presentation-layer adapters into the DI container.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> Binds strongly-typed configuration options and registers presentation-layer adapters (SignalR, Authentication, CORS, Rate Limiting, Exception Handling).</para>
    /// <para><b>Business &amp; Technical Justification:</b> Crucial for the platform MVP. Integrates security parameters (JWT Bearer keys, CORS policies) and establishes traffic throttling strategies to meet the SLA limits specified in the TRD.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Invoked during application startup before the WebApplication host is built. It prepares the container for resolving presentation requirements like HTTP user context.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Protects system resources from over-allocation, guarantees early startup failure on misconfigured options (via ValidateOnStart), and registers essential hub services.</para>
    /// </remarks>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddWebApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Fetch strongly-typed options for validation and binding
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        // ── Configuration Options Registration with Startup Validation ──────

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

        // ── Presentation Infrastructure Registrations ───────────────────────

        // Accessor to current HTTP context (used by Auditing and CQRS handlers)
        services.AddHttpContextAccessor();

        // Current user principal extractor (binds claims to application contexts)
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

        // Real-time dispatching mechanism utilizing SignalR
        services.AddScoped<IRealtimeNotificationService, SignalRRealtimeNotificationService>();

        // Secure HttpOnly cookie manager for Refresh Token rotations
        services.AddScoped<IRefreshTokenCookieService, RefreshTokenCookieService>();

        // ── Exception Handling & RFC 7807 problem details ───────────────────

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions["timestampUtc"] = DateTime.UtcNow;
            };
        });

        // ── Controllers and JSON Serialization ──────────────────────────────

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Camelcase naming policy for API standards compliance
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                
                // Ignore self-referential model cycles for clean tree structures
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

        // ── Authentication Engine Setup (JWT Bearer) ────────────────────────

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
                ClockSkew = TimeSpan.Zero // Strict timing rule: reject expired tokens instantly
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Support access token extraction from Query String for WebSocket connections (SignalR Hubs)
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

        // ── Authorization Policies ──────────────────────────────────────────

        services.AddAuthorization(options =>
        {
            options.AddPolicy("WorkspaceOwner", policy => policy.RequireClaim("workspace_role", "Owner"));
            options.AddPolicy("WorkspaceAdmin", policy => policy.RequireClaim("workspace_role", "Owner", "Admin"));
            options.AddPolicy("WorkspaceEditor", policy => policy.RequireClaim("workspace_role", "Owner", "Admin", "Editor"));
        });

        // ── CORS Policy Definition ──────────────────────────────────────────

        services.AddCors(options =>
        {
            // Dev CORS: Flexible to accommodate local test runners or wildcards
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

            // Production CORS: Highly strict matching configurations (prevent CSRF/CORS bypasses)
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

        // ── Rate Limiting Engine Registration ───────────────────────────────

        services.AddRateLimiter(options =>
        {
            // Fixed Window: standard API throttling (e.g. 60 requests per minute)
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.GeneralWindowSeconds);
                limiterOptions.PermitLimit = rateLimitOptions.GeneralPermitLimit;
                limiterOptions.QueueLimit = 0;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            // Token Bucket: specialized throttling for upload tasks (allows bursting up to capacity)
            options.AddTokenBucketLimiter("upload", limiterOptions =>
            {
                limiterOptions.TokenLimit = rateLimitOptions.UploadTokenLimit;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitOptions.UploadReplenishmentSeconds);
                limiterOptions.TokensPerPeriod = rateLimitOptions.UploadTokensPerPeriod;
                limiterOptions.AutoReplenishment = true;
            });

            // Customized rejection format complying with ProblemDetails standards
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

        // ── Realtime SignalR Engine ─────────────────────────────────────────

        var signalROptions = configuration.GetSection(SignalROptions.SectionName).Get<SignalROptions>()
            ?? new SignalROptions();

        var signalRBuilder = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.KeepAliveInterval = TimeSpan.FromSeconds(signalROptions.KeepAliveSeconds);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(signalROptions.ClientTimeoutSeconds);
            options.MaximumReceiveMessageSize = signalROptions.MaximumReceiveMessageSize;
        });

        // Redis-backed backplane for SignalR scale-out
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        if (!string.IsNullOrWhiteSpace(redisOptions?.ConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(redisOptions.ConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(signalROptions.ChannelPrefix);
            });
        }

        // ── API Metadata Documentation (Swagger) ────────────────────────────

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AutoPost API",
                Version = "v1",
                Description = "Multi-platform social publishing and automation API."
            });

            var bearerScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Description = "Enter a valid bearer token."
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new List<string>()
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Configures the complete Web API middleware pipeline.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> Assembles the HTTP request handling pipeline orchestrating middleware execution flow.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Crucial for enforcing consistent request/response life cycles. Assures security boundaries (Authentication before Rate Limiter, CORS first) are strictly maintained.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Serves as the operational run-time engine for each incoming HTTP call, routing request payloads through successive safety filters before reaching controllers.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals CORS and Rate Limiting restrictions, enforces global exception interceptors, and boots background job processors on app initialization.</para>
    /// </remarks>
    /// <param name="app">The web application instance.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for fluent chaining.</returns>
    public static WebApplication UseWebApiPipeline(this WebApplication app)
    {
        var signalROptions = app.Services.GetRequiredService<IOptions<SignalROptions>>().Value;
        var hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;

        // ── 1. Developer Exception and Interactive Specs ────────────────────
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
            // Security: Enforce HTTP Strict Transport Security (HSTS)
            app.UseHsts();
        }

        // ── 2. Global Exception Interception (Renders ProblemDetails) ───────
        app.UseExceptionHandler();

        // ── 3. Transport Level Security ─────────────────────────────────────
        app.UseHttpsRedirection();

        // ── 4. Strict Security Header Injection (OWASP Compliance) ──────────
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            await next();
        });

        // ── 5. Cross-Origin Resource Sharing (CORS) ─────────────────────────
        app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

        // ── 6. Auth Endpoint Brute-Force Rate Limiting ──────────────────────
        // Placed BEFORE Authentication to reject malicious bots early and save cryptographic CPU cycles.
        app.UseMiddleware<AuthRateLimitMiddleware>();

        // ── 7. Core Identity Authentication ─────────────────────────────────
        app.UseAuthentication();

        // ── 7.5 Custom Claims Extraction and Validation Middleware ──────────
        app.UseMiddleware<ClaimsValidationMiddleware>();

        // ── 8. General Purpose Rate Limiting (Fixed Window/Token Bucket) ────
        app.UseRateLimiter();

        // ── 9. Resource Level Authorization policies ────────────────────────
        app.UseAuthorization();

        // ── 10. Background Processing Dashboard ─────────────────────────────
        app.UseHangfireDashboard(hangfireOptions.DashboardPath, new Hangfire.DashboardOptions
        {
            // Security constraint: Dashboard is strictly READ-ONLY in Production environments.
            IsReadOnlyFunc = _ => !app.Environment.IsDevelopment()
        });

        // ── 11. Background Jobs Startup Registrations ───────────────────────
        using (var scope = app.Services.CreateScope())
        {
            // Resolves as Scoped correctly — no captured dependencies
            scope.ServiceProvider.GetRequiredService<IRecurringJobRegistrar>().RegisterRecurringJobs();
        }

        // ── 12. Endpoint Route Mapping ──────────────────────────────────────
        app.MapHealthChecks("/health");
        app.MapControllers();
        app.MapHub<NotificationHub>(signalROptions.NotificationHubPath);

        return app;
    }
}

