using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions.Integrations;
using Application.Abstractions.Security;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integrations;

/// <summary>
/// Implements provider-specific OAuth, profile and analytics calls behind normalized integration contracts.
/// </summary>
public sealed class PlatformIntegrationService : IPlatformIntegrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPlatformOAuthClientFactory _platformOAuthClientFactory;
    private readonly IOptions<PlatformOAuthOptions> _platformOptions;
    private readonly ITokenProtectionService _tokenProtectionService;
    private readonly ILogger<PlatformIntegrationService> _logger;

    /// <summary>
    /// Initializes the integration service with provider clients and configuration.
    /// </summary>
    /// <param name="platformOAuthClientFactory">Factory used to resolve typed provider clients.</param>
    /// <param name="platformOptions">Provider OAuth and API configuration.</param>
    /// <param name="tokenProtectionService">Data-protection service used to unwrap provider credentials.</param>
    /// <param name="logger">Structured logger for diagnostics.</param>
    public PlatformIntegrationService(
        IPlatformOAuthClientFactory platformOAuthClientFactory,
        IOptions<PlatformOAuthOptions> platformOptions,
        ITokenProtectionService tokenProtectionService,
        ILogger<PlatformIntegrationService> logger)
    {
        _platformOAuthClientFactory = platformOAuthClientFactory;
        _platformOptions = platformOptions;
        _tokenProtectionService = tokenProtectionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string BuildAuthorizationUrl(Platform platform, string redirectUri, string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var provider = GetProvider(platform);
        var uriBuilder = new UriBuilder(provider.AuthorizationEndpoint);
        var scopes = string.Join(' ', provider.Scopes ?? []);
        var query = new List<string>
        {
            $"client_id={Uri.EscapeDataString(provider.ClientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"state={Uri.EscapeDataString(state)}"
        };

        if (!string.IsNullOrWhiteSpace(scopes))
        {
            query.Add($"scope={Uri.EscapeDataString(scopes)}");
        }

        query.Add("access_type=offline");
        query.Add("prompt=consent");
        uriBuilder.Query = string.Join("&", query);
        return uriBuilder.ToString();
    }

    /// <inheritdoc />
    public async Task<PlatformConnectionResult> ExchangeCodeAsync(
        Platform platform,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);

        var provider = GetProvider(platform);
        var client = _platformOAuthClientFactory.CreateClient(platform).HttpClient;
        using var request = new HttpRequestMessage(HttpMethod.Post, provider.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = provider.ClientId,
                ["client_secret"] = provider.ClientSecret
            })
        };

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"OAuth token response from {platform} could not be parsed.");

        var expiresAtUtc = DateTime.UtcNow.AddSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 3600);
        var profile = await GetProfileByAccessTokenAsync(platform, payload.AccessToken, ct);
        return new PlatformConnectionResult(
            payload.AccessToken,
            payload.RefreshToken,
            expiresAtUtc,
            string.IsNullOrWhiteSpace(payload.Scope)
                ? string.Join(',', provider.Scopes ?? [])
                : payload.Scope.Replace(' ', ','),
            profile);
    }

    /// <inheritdoc />
    public async Task<PlatformAccountProfile> GetAccountProfileAsync(SocialAccount socialAccount, CancellationToken ct = default)
    {
        var accessToken = _tokenProtectionService.Unprotect(
            socialAccount.EncryptedAccessToken,
            GetAccessTokenPurpose(socialAccount.Platform));

        return await GetProfileByAccessTokenAsync(socialAccount.Platform, accessToken, ct);
    }

    /// <inheritdoc />
    public async Task<PlatformAccountInsightSnapshot> GetAccountInsightAsync(SocialAccount socialAccount, CancellationToken ct = default)
    {
        var profile = await GetAccountProfileAsync(socialAccount, ct);
        return new PlatformAccountInsightSnapshot(
            DateTime.UtcNow,
            profile.FollowersCount ?? 0,
            0,
            0,
            null,
            null);
    }

    /// <inheritdoc />
    public Task<PlatformPostAnalyticsSnapshot> GetPostAnalyticsAsync(
        SocialAccount socialAccount,
        string remotePostId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePostId);

        _logger.LogInformation(
            "Provider-specific post analytics endpoint is not configured for platform {Platform}; returning a zero snapshot for remote post {RemotePostId}.",
            socialAccount.Platform,
            remotePostId);

        return Task.FromResult(new PlatformPostAnalyticsSnapshot(
            DateTime.UtcNow,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            null,
            null));
    }

    private async Task<PlatformAccountProfile> GetProfileByAccessTokenAsync(Platform platform, string accessToken, CancellationToken ct)
    {
        var client = _platformOAuthClientFactory.CreateClient(platform).HttpClient;
        using var request = BuildProfileRequest(platform, accessToken);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return platform switch
        {
            Platform.YouTube => ParseYouTubeProfile(document),
            Platform.Instagram => ParseInstagramProfile(document),
            Platform.Facebook => ParseFacebookProfile(document),
            Platform.TikTok => ParseTikTokProfile(document),
            Platform.Twitter => ParseTwitterProfile(document),
            Platform.Telegram => ParseTelegramProfile(document),
            _ => throw new NotSupportedException($"Platform '{platform}' is not supported.")
        };
    }

    private static HttpRequestMessage BuildProfileRequest(Platform platform, string accessToken)
    {
        var request = platform switch
        {
            Platform.YouTube => new HttpRequestMessage(HttpMethod.Get, "youtube/v3/channels?part=snippet,statistics&mine=true"),
            Platform.Instagram => new HttpRequestMessage(HttpMethod.Get, "v19.0/me?fields=id,username,account_type,media_count,followers_count"),
            Platform.Facebook => new HttpRequestMessage(HttpMethod.Get, "v19.0/me?fields=id,name,picture{url}"),
            Platform.TikTok => new HttpRequestMessage(HttpMethod.Get, "v2/user/info/?fields=open_id,display_name,avatar_url,follower_count"),
            Platform.Twitter => new HttpRequestMessage(HttpMethod.Get, "2/users/me?user.fields=profile_image_url,public_metrics,username,name"),
            Platform.Telegram => new HttpRequestMessage(HttpMethod.Get, $"bot{accessToken}/getMe"),
            _ => throw new NotSupportedException($"Platform '{platform}' is not supported.")
        };

        if (platform is not Platform.Telegram)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    private static PlatformAccountProfile ParseYouTubeProfile(JsonDocument document)
    {
        var root = document.RootElement;
        if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("YouTube profile response did not contain any channels.");
        }

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        var statistics = item.TryGetProperty("statistics", out var stats) ? stats : default;
        return new PlatformAccountProfile(
            item.GetProperty("id").GetString() ?? throw new InvalidOperationException("YouTube channel id is missing."),
            snippet.GetProperty("title").GetString() ?? "YouTube Account",
            snippet.TryGetProperty("customUrl", out var customUrl) ? customUrl.GetString() : null,
            snippet.TryGetProperty("thumbnails", out var thumbnails) && thumbnails.TryGetProperty("default", out var thumb) && thumb.TryGetProperty("url", out var url)
                ? url.GetString()
                : null,
            "Channel",
            false,
            statistics.ValueKind != JsonValueKind.Undefined && statistics.TryGetProperty("subscriberCount", out var count) && long.TryParse(count.GetString(), out var parsed)
                ? parsed
                : null);
    }

    private static PlatformAccountProfile ParseInstagramProfile(JsonDocument document)
    {
        var root = document.RootElement;
        return new PlatformAccountProfile(
            root.GetProperty("id").GetString() ?? throw new InvalidOperationException("Instagram id is missing."),
            root.TryGetProperty("username", out var username) ? username.GetString() ?? "Instagram Account" : "Instagram Account",
            root.TryGetProperty("username", out username) ? username.GetString() : null,
            null,
            root.TryGetProperty("account_type", out var accountType) ? accountType.GetString() : null,
            false,
            root.TryGetProperty("followers_count", out var followers) ? followers.GetInt64() : null);
    }

    private static PlatformAccountProfile ParseFacebookProfile(JsonDocument document)
    {
        var root = document.RootElement;
        var avatarUrl = root.TryGetProperty("picture", out var picture) &&
                        picture.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("url", out var url)
            ? url.GetString()
            : null;

        return new PlatformAccountProfile(
            root.GetProperty("id").GetString() ?? throw new InvalidOperationException("Facebook id is missing."),
            root.GetProperty("name").GetString() ?? "Facebook Account",
            null,
            avatarUrl,
            "Page",
            false,
            null);
    }

    private static PlatformAccountProfile ParseTikTokProfile(JsonDocument document)
    {
        var user = document.RootElement.GetProperty("data").GetProperty("user");
        return new PlatformAccountProfile(
            user.GetProperty("open_id").GetString() ?? throw new InvalidOperationException("TikTok open_id is missing."),
            user.TryGetProperty("display_name", out var displayName) ? displayName.GetString() ?? "TikTok Account" : "TikTok Account",
            user.TryGetProperty("username", out var username) ? username.GetString() : null,
            user.TryGetProperty("avatar_url", out var avatarUrl) ? avatarUrl.GetString() : null,
            "Account",
            false,
            user.TryGetProperty("follower_count", out var followers) ? followers.GetInt64() : null);
    }

    private static PlatformAccountProfile ParseTwitterProfile(JsonDocument document)
    {
        var data = document.RootElement.GetProperty("data");
        long? followers = null;
        if (data.TryGetProperty("public_metrics", out var metrics) &&
            metrics.TryGetProperty("followers_count", out var followerCount))
        {
            followers = followerCount.GetInt64();
        }

        return new PlatformAccountProfile(
            data.GetProperty("id").GetString() ?? throw new InvalidOperationException("Twitter user id is missing."),
            data.TryGetProperty("name", out var name) ? name.GetString() ?? "Twitter Account" : "Twitter Account",
            data.TryGetProperty("username", out var username) ? username.GetString() : null,
            data.TryGetProperty("profile_image_url", out var avatarUrl) ? avatarUrl.GetString() : null,
            "Account",
            false,
            followers);
    }

    private static PlatformAccountProfile ParseTelegramProfile(JsonDocument document)
    {
        var result = document.RootElement.GetProperty("result");
        var username = result.TryGetProperty("username", out var usernameElement) ? usernameElement.GetString() : null;
        var displayName = result.TryGetProperty("first_name", out var firstName) ? firstName.GetString() : "Telegram Bot";

        return new PlatformAccountProfile(
            result.GetProperty("id").GetRawText(),
            displayName ?? "Telegram Bot",
            username,
            null,
            "Bot",
            false,
            null);
    }

    private OAuthProviderOptions GetProvider(Platform platform) => platform switch
    {
        Platform.YouTube => _platformOptions.Value.YouTube,
        Platform.Instagram => _platformOptions.Value.Instagram,
        Platform.Facebook => _platformOptions.Value.Facebook,
        Platform.TikTok => _platformOptions.Value.TikTok,
        Platform.Twitter => _platformOptions.Value.Twitter,
        Platform.Telegram => _platformOptions.Value.Telegram,
        _ => throw new NotSupportedException($"Platform '{platform}' is not supported.")
    };

    private static string GetAccessTokenPurpose(Platform platform) => $"{platform}:access-token";

    private sealed record TokenPayload(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
