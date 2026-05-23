namespace Application.DTOs.SocialAccount;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  SOCIAL ACCOUNT DTOs — OAuth Integrations, Connected Platforms             ║
// ║  TRD Stage 3: OAuth & Multi-platform                                       ║
// ║  Endpoints: GET/DELETE /api/social-accounts, .../connect, .../callback     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Connected social account data returned by the social account endpoints.
///
/// <para><b>What it contains:</b>
/// Public information about a connected social account: platform, display name, avatar,
/// connection status, and follower count. NEVER exposes OAuth tokens — those are encrypted
/// in the database and handled entirely server-side.</para>
///
/// <para><b>Security:</b>
/// AccessToken, RefreshToken, and TokenExpiresAt are intentionally excluded.
/// These are sensitive credentials managed exclusively by the Infrastructure layer.</para>
///
/// <para><b>TRD API:</b> GET /api/social-accounts</para>
/// </summary>
/// <param name="Id">The social account's unique identifier (internal, not platform-side).</param>
/// <param name="Platform">Platform name as string: "YouTube", "Instagram", "TikTok", "Facebook", "Twitter", "Telegram".</param>
/// <param name="ExternalAccountId">The platform-side account/channel identifier (e.g., YouTube channel ID).</param>
/// <param name="AccountDisplayName">Display name on the platform (e.g., "TechReviews Channel").</param>
/// <param name="AccountUsername">Username/handle on the platform (e.g., "@techreviews"), or null if not applicable.</param>
/// <param name="AccountAvatarUrl">URL to the account's avatar/profile picture on the platform, or null.</param>
/// <param name="Status">Connection status as string: "Active", "TokenExpired", "Revoked", "Disconnected".</param>
/// <param name="IsPrivateAccount">Whether the account is set to private on the platform. Affects DM automation capabilities.</param>
/// <param name="FollowersCount">Current follower/subscriber count, or null if not yet fetched.</param>
/// <param name="GrantedScopes">Array of OAuth scopes granted by the user during authorization (e.g., ["publish", "read_insights"]).</param>
/// <param name="ConnectedAt">UTC timestamp when the account was first connected via OAuth.</param>
public record SocialAccountDto(
    Guid Id,
    string Platform,
    string ExternalAccountId,
    string AccountDisplayName,
    string? AccountUsername,
    string? AccountAvatarUrl,
    string Status,
    bool IsPrivateAccount,
    long? FollowersCount,
    string[] GrantedScopes,
    DateTime ConnectedAt
);

/// <summary>
/// OAuth redirect URL returned when the user initiates a platform connection.
///
/// <para><b>What it does:</b>
/// Contains the full OAuth authorization URL that the frontend redirects the user to.
/// The State parameter is a CSRF token that must be stored in an encrypted cookie
/// and verified when the platform calls back.</para>
///
/// <para><b>Security — CSRF protection:</b>
/// The State value is generated server-side (Guid.NewGuid()), stored in a secure cookie,
/// and validated during the callback. If the State doesn't match, the callback is rejected
/// as a potential CSRF attack.</para>
///
/// <para><b>TRD API:</b> GET /api/social-accounts/connect/{platform}</para>
/// </summary>
/// <param name="Url">The full OAuth authorization URL to redirect the user to (includes client_id, redirect_uri, scopes).</param>
/// <param name="State">CSRF protection token. Must be stored in an encrypted HttpOnly cookie before redirecting.</param>
public record OAuthUrlDto(
    string Url,
    string State
);
