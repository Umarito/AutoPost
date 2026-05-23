using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  AUTH DTOs — Registration, Login, Token Management, User Profile            ║
// ║  TRD Stage 1: Auth & Workspace                                             ║
// ║  Endpoints: POST /api/auth/register, /login, /refresh, /logout             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for user registration.
///
/// <para><b>What it does:</b>
/// Carries the minimum required information to create a new user account.
/// The service creates both an ApplicationUser and a default Workspace (with the user as Owner).</para>
///
/// <para><b>Security:</b>
/// The password is validated for strength by FluentValidation / Identity rules.
/// It is NEVER stored in plaintext — Identity hashes it using PBKDF2.</para>
///
/// <para><b>TRD API:</b> POST /api/auth/register</para>
/// </summary>
/// <param name="Email">The user's email address. Used as the primary login credential. Must be unique across the system.</param>
/// <param name="Password">The user's chosen password. Must meet strength requirements (min 8 chars, mixed case, digit, special).</param>
/// <param name="DisplayName">The user's display name shown in the UI (team member lists, post author, inbox sender).</param>
public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(200)] string DisplayName
);

/// <summary>
/// Payload for user authentication.
///
/// <para><b>What it does:</b>
/// Carries email/password credentials for the login flow. The service verifies
/// the password against the Identity store and issues a JWT + RefreshToken pair.</para>
///
/// <para><b>TRD API:</b> POST /api/auth/login</para>
/// </summary>
/// <param name="Email">The registered email address.</param>
/// <param name="Password">The user's password (verified via Identity, never logged or stored).</param>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

/// <summary>
/// Payload for updating user profile information.
///
/// <para><b>What it does:</b>
/// Allows the user to update their display name, avatar, timezone, and locale.
/// All fields are optional — only non-null values are applied (PATCH semantics).</para>
///
/// <para><b>TRD API:</b> PUT /api/auth/profile</para>
/// </summary>
/// <param name="DisplayName">New display name (shown in team lists and inbox).</param>
/// <param name="AvatarUrl">URL to the user's avatar image (hosted in CDN/Storage).</param>
/// <param name="TimeZoneId">IANA timezone identifier (e.g., "Europe/Moscow"). Used for scheduling display.</param>
/// <param name="Locale">User's preferred locale for UI language (e.g., "ru-RU", "en-US").</param>
public record UpdateProfileRequest(
    [MaxLength(200)] string? DisplayName,
    [MaxLength(2048)] string? AvatarUrl,
    [MaxLength(100)] string? TimeZoneId,
    [MaxLength(10)] string? Locale
);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Authentication tokens returned after successful login or token refresh.
///
/// <para><b>What it contains:</b>
/// A short-lived JWT access token (15 min) for API authorization, and a long-lived
/// refresh token (7 days) for silent re-authentication. The client stores the refresh
/// token in an HttpOnly cookie (never localStorage) for security.</para>
///
/// <para><b>How it works:</b>
/// When the access token expires, the client calls POST /api/auth/refresh with the
/// refresh token to get a new pair. The old refresh token is invalidated (rotation).</para>
///
/// <para><b>TRD Security:</b> "JWT with short TTL (15 min) + Refresh Token with rotation."</para>
/// </summary>
/// <param name="AccessToken">JWT access token for API authorization (passed in Authorization: Bearer header).</param>
/// <param name="RefreshToken">Opaque refresh token for obtaining new access tokens. Stored as SHA-256 hash in DB.</param>
/// <param name="ExpiresAt">UTC timestamp when the access token expires. The client uses this to proactively refresh.</param>
public record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt
);

/// <summary>
/// User profile data returned by the GET /api/auth/profile endpoint.
///
/// <para><b>What it contains:</b>
/// All public profile information about the current user. Excludes sensitive data
/// like password hashes, security stamps, and internal Identity fields.</para>
///
/// <para><b>Security:</b>
/// This DTO is specifically designed to NEVER expose password hashes, security stamps,
/// or any Identity-internal data. Only domain-level profile fields are included.</para>
/// </summary>
/// <param name="Id">The user's unique identifier (Guid).</param>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's display name (shown in UI).</param>
/// <param name="AvatarUrl">URL to the user's avatar image, or null if not set.</param>
/// <param name="TimeZoneId">The user's IANA timezone (e.g., "Europe/Moscow"), or null if using system default.</param>
/// <param name="Locale">The user's UI locale (e.g., "ru-RU"), or null if using system default.</param>
/// <param name="IsActive">Whether the account is active (false = suspended by admin).</param>
/// <param name="LastLoginAt">UTC timestamp of the user's most recent login, or null if never logged in.</param>
/// <param name="CreatedAt">UTC timestamp when the account was created.</param>
public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? TimeZoneId,
    string? Locale,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);
