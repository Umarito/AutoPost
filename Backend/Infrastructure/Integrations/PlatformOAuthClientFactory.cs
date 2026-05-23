using Application.Abstractions.Integrations;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Integrations;

/// <summary>
/// Resolves typed outbound platform clients from the dependency injection container.
/// </summary>
public sealed class PlatformOAuthClientFactory : IPlatformOAuthClientFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the factory with the root service provider used for typed client resolution.
    /// </summary>
    /// <param name="serviceProvider">The service provider that owns the typed clients.</param>
    public PlatformOAuthClientFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IPlatformOAuthClient CreateClient(Platform platform) => platform switch
    {
        Platform.YouTube => _serviceProvider.GetRequiredService<YouTubePlatformOAuthClient>(),
        Platform.Instagram => _serviceProvider.GetRequiredService<InstagramPlatformOAuthClient>(),
        Platform.Facebook => _serviceProvider.GetRequiredService<FacebookPlatformOAuthClient>(),
        Platform.TikTok => _serviceProvider.GetRequiredService<TikTokPlatformOAuthClient>(),
        Platform.Twitter => _serviceProvider.GetRequiredService<TwitterPlatformOAuthClient>(),
        Platform.Telegram => _serviceProvider.GetRequiredService<TelegramPlatformOAuthClient>(),
        _ => throw new NotSupportedException($"Platform '{platform}' is not supported.")
    };
}
