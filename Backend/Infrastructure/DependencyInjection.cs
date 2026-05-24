using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Media;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.RateLimiting;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Abstractions.Storage;
using Application.Abstractions.Webhooks;
using Domain.Entities;
using Hangfire;
using Hangfire.PostgreSql;
using Infrastructure.BackgroundJobs;
using Infrastructure.Caching;
using Infrastructure.Data;
using Infrastructure.HealthChecks;
using Infrastructure.Integrations;
using Infrastructure.Notifications;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.RateLimiting;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Storage;
using Infrastructure.Webhooks;
using Infrastructure.Media;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;

namespace Infrastructure;

/// <summary>
/// Registers all Infrastructure-layer services required by the AutoPost platform.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure services, integrations and technical capabilities into the DI container.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddInfrastructureOptions(configuration)
            .AddPersistence(configuration)
            .AddIdentity()
            .AddCaching(configuration)
            .AddBackgroundProcessing(configuration)
            .AddExternalIntegrations()
            .AddRepositories();

        return services;
    }

    private static IServiceCollection AddInfrastructureOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HangfireOptions>()
            .Bind(configuration.GetSection(HangfireOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.Queues.Length > 0, "At least one Hangfire queue must be configured.")
            .ValidateOnStart();

        services.AddOptions<AzureBlobStorageOptions>()
            .Bind(configuration.GetSection(AzureBlobStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PlatformOAuthOptions>()
            .Bind(configuration.GetSection(PlatformOAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<WebhookOptions>()
            .Bind(configuration.GetSection(WebhookOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                defaultConnection,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                }));

        services.AddDataProtection()
            .SetApplicationName("AutoPost")
            .PersistKeysToDbContext<ApplicationDbContext>();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("postgresql")
            .AddCheck<RedisConnectionHealthCheck>("redis");

        return services;
    }

    private static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenHasher, RefreshTokenHasher>();
        services.AddScoped<ITokenProtectionService, TokenProtectionService>();

        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? throw new InvalidOperationException("Redis configuration is required.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisOptions.ConnectionString;
            options.InstanceName = redisOptions.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IRedisRateLimitService, RedisRateLimitService>();

        return services;
    }

    private static IServiceCollection AddBackgroundProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        var hangfireOptions = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
            ?? throw new InvalidOperationException("Hangfire configuration is required.");

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(storageOptions =>
                {
                    storageOptions.UseNpgsqlConnection(hangfireOptions.ConnectionString);
                });
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = hangfireOptions.WorkerCount;
            options.Queues = hangfireOptions.Queues;
        });

        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        services.AddScoped<IRecurringJobRegistrar, InfrastructureRecurringJobRegistrar>();
        services.AddScoped<InfrastructureHeartbeatJob>();
        services.AddScoped<SendEmailJob>();
        services.AddScoped<SendPushNotificationJob>();

        return services;
    }

    private static IServiceCollection AddExternalIntegrations(this IServiceCollection services)
    {
        services.AddSingleton(serviceProvider =>
        {
            var blobOptions = serviceProvider.GetRequiredService<IOptions<AzureBlobStorageOptions>>().Value;
            return new Azure.Storage.Blobs.BlobServiceClient(blobOptions.ConnectionString);
        });

        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
        services.AddScoped<IPlatformOAuthClientFactory, PlatformOAuthClientFactory>();
        services.AddScoped<IPlatformIntegrationService, PlatformIntegrationService>();
        services.AddScoped<IPlatformTokenValidationService, PlatformTokenValidationService>();
        services.AddScoped<IPlatformPublisherFactory, PlatformPublisherFactory>();
        services.AddScoped<IPlatformMessagingService, PlatformMessagingService>();
        services.AddScoped<DefaultPlatformPublisher>();
        services.AddScoped<IInviteTokenService, InviteTokenService>();
        services.AddScoped<SmtpEmailTransport>();
        services.AddScoped<IEmailService, HangfireEmailService>();
        services.AddScoped<PushNotificationTransport>();
        services.AddScoped<IPushNotificationService, HangfirePushNotificationService>();
        services.AddScoped<IWebhookPayloadParserFactory, WebhookPayloadParserFactory>();
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.YouTube));
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.Instagram));
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.Facebook));
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.TikTok));
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.Twitter));
        services.AddScoped<IWebhookPayloadParser>(_ => new DefaultWebhookPayloadParser(Domain.Enums.Platform.Telegram));
        services.AddScoped<IVideoMetadataExtractor, FfprobeVideoMetadataExtractor>();
        services.AddScoped<IVideoProcessingService, BlobBackedVideoProcessingService>();

        services.AddHttpClient<YouTubePlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.YouTube))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<InstagramPlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.Instagram))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<FacebookPlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.Facebook))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<TikTokPlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.TikTok))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<TwitterPlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.Twitter))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<TelegramPlatformOAuthClient>((serviceProvider, client) =>
                ConfigurePlatformClient(client, serviceProvider.GetRequiredService<IOptions<PlatformOAuthOptions>>().Value.Telegram))
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IApplicationUserRepository, ApplicationUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceMemberRepository, WorkspaceMemberRepository>();
        services.AddScoped<ISocialAccountRepository, SocialAccountRepository>();
        services.AddScoped<ISocialAccountInsightRepository, SocialAccountInsightRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IPostTargetRepository, PostTargetRepository>();
        services.AddScoped<IPublishingJobRepository, PublishingJobRepository>();
        services.AddScoped<IPostAnalyticsSnapshotRepository, PostAnalyticsSnapshotRepository>();
        services.AddScoped<IInboxConversationRepository, InboxConversationRepository>();
        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IConversationAssignmentRepository, ConversationAssignmentRepository>();
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<IAutomationExecutionLogRepository, AutomationExecutionLogRepository>();
        services.AddScoped<IPendingDMQueueRepository, PendingDMQueueRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<INotificationHistoryRepository, NotificationHistoryRepository>();

        return services;
    }

    private static void ConfigurePlatformClient(HttpClient client, OAuthProviderOptions providerOptions)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AutoPost", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (Uri.TryCreate(providerOptions.ApiBaseUrl, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        => HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        => HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 2,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
