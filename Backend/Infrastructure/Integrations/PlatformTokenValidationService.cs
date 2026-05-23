using System.Net.Http.Json;
using Application.Abstractions.Integrations;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Infrastructure.Integrations;

/// <summary>
/// Validates and refreshes provider access tokens before sensitive outbound platform operations.
/// </summary>
public sealed class PlatformTokenValidationService : IPlatformTokenValidationService
{
    private readonly IPlatformOAuthClientFactory _platformOAuthClientFactory;
    private readonly ITokenProtectionService _tokenProtectionService;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IOptions<PlatformOAuthOptions> _platformOptions;
    private readonly ILogger<PlatformTokenValidationService> _logger;

    /// <summary>
    /// Initializes the token-validation service.
    /// </summary>
    /// <param name="platformOAuthClientFactory">Factory used to resolve typed provider clients.</param>
    /// <param name="tokenProtectionService">Service used to protect refreshed credentials.</param>
    /// <param name="socialAccountRepository">Repository used to mark social-account state changes.</param>
    /// <param name="platformOptions">Provider OAuth configuration.</param>
    /// <param name="logger">Structured logger for diagnostics.</param>
    public PlatformTokenValidationService(
        IPlatformOAuthClientFactory platformOAuthClientFactory,
        ITokenProtectionService tokenProtectionService,
        ISocialAccountRepository socialAccountRepository,
        IOptions<PlatformOAuthOptions> platformOptions,
        ILogger<PlatformTokenValidationService> logger)
    {
        _platformOAuthClientFactory = platformOAuthClientFactory;
        _tokenProtectionService = tokenProtectionService;
        _socialAccountRepository = socialAccountRepository;
        _platformOptions = platformOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PlatformTokenValidationResult> EnsureValidAsync(SocialAccount socialAccount, CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        if (!socialAccount.IsTokenExpired(utcNow.AddMinutes(1)))
        {
            socialAccount.MarkActive();
            _socialAccountRepository.Update(socialAccount);
            return new PlatformTokenValidationResult(true, false, socialAccount.TokenExpiresAt, null);
        }

        if (string.IsNullOrWhiteSpace(socialAccount.EncryptedRefreshToken))
        {
            socialAccount.MarkTokenExpired();
            _socialAccountRepository.Update(socialAccount);
            return new PlatformTokenValidationResult(false, false, socialAccount.TokenExpiresAt, "The provider access token has expired and no refresh token is available.");
        }

        try
        {
            var provider = GetProvider(socialAccount.Platform);
            var refreshToken = _tokenProtectionService.Unprotect(
                socialAccount.EncryptedRefreshToken,
                $"{socialAccount.Platform}:refresh-token");

            var client = _platformOAuthClientFactory.CreateClient(socialAccount.Platform).HttpClient;
            using var response = await client.PostAsync(
                provider.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = provider.ClientId,
                    ["client_secret"] = provider.ClientSecret
                }),
                ct);

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<RefreshPayload>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Provider refresh-token response could not be parsed.");

            var expiresAtUtc = DateTime.UtcNow.AddSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 3600);
            socialAccount.UpdateCredentials(
                _tokenProtectionService.Protect(payload.AccessToken, $"{socialAccount.Platform}:access-token"),
                string.IsNullOrWhiteSpace(payload.RefreshToken)
                    ? socialAccount.EncryptedRefreshToken
                    : _tokenProtectionService.Protect(payload.RefreshToken, $"{socialAccount.Platform}:refresh-token"),
                expiresAtUtc,
                string.IsNullOrWhiteSpace(payload.Scope)
                    ? socialAccount.GrantedScopes
                    : payload.Scope.Replace(' ', ','));

            _socialAccountRepository.Update(socialAccount);
            return new PlatformTokenValidationResult(true, true, expiresAtUtc, null);
        }
        catch (Exception exception)
        {
            socialAccount.MarkTokenExpired();
            _socialAccountRepository.Update(socialAccount);
            _logger.LogWarning(exception, "Provider token refresh failed for social account {SocialAccountId}.", socialAccount.Id);
            return new PlatformTokenValidationResult(false, false, socialAccount.TokenExpiresAt, "The provider access token could not be refreshed.");
        }
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

    private sealed record RefreshPayload(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
