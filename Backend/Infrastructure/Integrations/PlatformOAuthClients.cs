using Application.Abstractions.Integrations;
using Domain.Enums;

namespace Infrastructure.Integrations;

/// <summary>
/// Base class for typed platform clients backed by a configured <see cref="HttpClient"/>.
/// </summary>
public abstract class PlatformOAuthClientBase : IPlatformOAuthClient
{
    /// <summary>
    /// Initializes the platform client with the configured HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured for the target platform.</param>
    protected PlatformOAuthClientBase(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    /// <inheritdoc />
    public abstract Platform Platform { get; }

    /// <inheritdoc />
    public HttpClient HttpClient { get; }
}

/// <summary>Typed outbound client for YouTube OAuth and API calls.</summary>
public sealed class YouTubePlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.YouTube;
}

/// <summary>Typed outbound client for Instagram OAuth and API calls.</summary>
public sealed class InstagramPlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.Instagram;
}

/// <summary>Typed outbound client for Facebook OAuth and API calls.</summary>
public sealed class FacebookPlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.Facebook;
}

/// <summary>Typed outbound client for TikTok OAuth and API calls.</summary>
public sealed class TikTokPlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.TikTok;
}

/// <summary>Typed outbound client for Twitter/X OAuth and API calls.</summary>
public sealed class TwitterPlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.Twitter;
}

/// <summary>Typed outbound client for Telegram API calls.</summary>
public sealed class TelegramPlatformOAuthClient(HttpClient httpClient) : PlatformOAuthClientBase(httpClient)
{
    /// <inheritdoc />
    public override Platform Platform => Platform.Telegram;
}
