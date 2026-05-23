using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Coordinates provider-specific OAuth, profile, account insight and post analytics operations.
/// </summary>
public interface IPlatformIntegrationService
{
    /// <summary>
    /// Builds the authorization URL that should be used to start an OAuth connection flow.
    /// </summary>
    /// <param name="platform">Target external platform.</param>
    /// <param name="redirectUri">Absolute redirect URI that the provider should call back.</param>
    /// <param name="state">Protected CSRF state payload.</param>
    /// <returns>The provider-specific authorization URL.</returns>
    string BuildAuthorizationUrl(Platform platform, string redirectUri, string state);

    /// <summary>
    /// Exchanges an OAuth authorization code for provider credentials and resolves the connected account profile.
    /// </summary>
    /// <param name="platform">Target external platform.</param>
    /// <param name="authorizationCode">Authorization code returned by the provider.</param>
    /// <param name="redirectUri">Redirect URI that was used during the authorization flow.</param>
    /// <param name="ct">Cancellation token for outbound network I/O.</param>
    /// <returns>Normalized provider credentials and public profile metadata.</returns>
    Task<PlatformConnectionResult> ExchangeCodeAsync(
        Platform platform,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the latest public profile metadata for an already connected account.
    /// </summary>
    /// <param name="socialAccount">Connected social account whose metadata should be refreshed.</param>
    /// <param name="ct">Cancellation token for outbound network I/O.</param>
    /// <returns>Normalized public profile metadata.</returns>
    Task<PlatformAccountProfile> GetAccountProfileAsync(SocialAccount socialAccount, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one account-level insight snapshot for a connected social account.
    /// </summary>
    /// <param name="socialAccount">Connected social account whose insight metrics should be sampled.</param>
    /// <param name="ct">Cancellation token for outbound network I/O.</param>
    /// <returns>Normalized account insight metrics.</returns>
    Task<PlatformAccountInsightSnapshot> GetAccountInsightAsync(SocialAccount socialAccount, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one post-level analytics snapshot for a published remote post.
    /// </summary>
    /// <param name="socialAccount">Connected social account that owns the remote post.</param>
    /// <param name="remotePostId">Provider-side published post identifier.</param>
    /// <param name="ct">Cancellation token for outbound network I/O.</param>
    /// <returns>Normalized post analytics metrics.</returns>
    Task<PlatformPostAnalyticsSnapshot> GetPostAnalyticsAsync(
        SocialAccount socialAccount,
        string remotePostId,
        CancellationToken ct = default);
}
