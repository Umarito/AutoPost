using Application.Abstractions.Integrations;
using Application.Abstractions.Repositories;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

/// <summary>
/// Resolves platform-specific publishers.
/// </summary>
public sealed class PlatformPublisherFactory : IPlatformPublisherFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the factory with the current DI scope.
    /// </summary>
    /// <param name="serviceProvider">Scope-local service provider used to resolve publishers.</param>
    public PlatformPublisherFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IPlatformPublisher Create(Platform platform) => _serviceProvider.GetRequiredService<DefaultPlatformPublisher>();
}

/// <summary>
/// Default safe publisher implementation that validates ownership and credentials before reporting unsupported endpoints.
/// </summary>
public sealed class DefaultPlatformPublisher : IPlatformPublisher
{
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IPlatformTokenValidationService _platformTokenValidationService;
    private readonly ILogger<DefaultPlatformPublisher> _logger;

    /// <summary>
    /// Initializes the default publisher with dependencies required for guarded publishing.
    /// </summary>
    /// <param name="socialAccountRepository">Repository used to load the connected account.</param>
    /// <param name="platformTokenValidationService">Credential validation service executed before any outbound publish attempt.</param>
    /// <param name="logger">Structured logger for diagnostics.</param>
    public DefaultPlatformPublisher(
        ISocialAccountRepository socialAccountRepository,
        IPlatformTokenValidationService platformTokenValidationService,
        ILogger<DefaultPlatformPublisher> logger)
    {
        _socialAccountRepository = socialAccountRepository;
        _platformTokenValidationService = platformTokenValidationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Platform Platform => Platform.YouTube;

    /// <inheritdoc />
    public async Task<PlatformPublishResult> PublishAsync(PlatformPublishRequest request, CancellationToken ct = default)
    {
        var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, ct);
        if (account is null || account.WorkspaceId != request.WorkspaceId)
        {
            return new PlatformPublishResult(
                false,
                null,
                null,
                "Connected social account was not found inside the current workspace.",
                null);
        }

        var validation = await _platformTokenValidationService.EnsureValidAsync(account, ct);
        if (!validation.IsValid)
        {
            return new PlatformPublishResult(
                false,
                null,
                null,
                validation.FailureReason ?? "Provider credentials are not valid for publishing.",
                null);
        }

        _logger.LogWarning(
            "Publishing for platform {Platform} was requested for target {PostTargetId}, but no provider-specific publish endpoint is configured in the current MVP infrastructure.",
            request.Platform,
            request.PostTargetId);

        return new PlatformPublishResult(
            false,
            null,
            null,
            $"Publishing for platform '{request.Platform}' is not configured in the current provider adapter.",
            null);
    }
}
