using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Normalized publishing payload consumed by platform-specific publishers.
/// </summary>
/// <param name="PostId">The internal AutoPost post identifier.</param>
/// <param name="PostTargetId">The specific post target being published.</param>
/// <param name="WorkspaceId">The tenant boundary used for auditing and provider context.</param>
/// <param name="Platform">The target platform resolved from the social account.</param>
/// <param name="SocialAccountId">The connected social account that should publish the content.</param>
/// <param name="Caption">Primary text body or caption prepared for the platform.</param>
/// <param name="MediaUrl">Optional URL of the media asset that should be published.</param>
/// <param name="ScheduledAtUtc">The UTC schedule time that triggered the publication attempt.</param>
public sealed record PlatformPublishRequest(
    Guid PostId,
    Guid PostTargetId,
    Guid WorkspaceId,
    Platform Platform,
    Guid SocialAccountId,
    string? Caption,
    string? MediaUrl,
    DateTime ScheduledAtUtc);

/// <summary>
/// Normalized result returned from a platform publishing attempt.
/// </summary>
/// <param name="IsSuccess">Whether the remote API accepted the publication request.</param>
/// <param name="RemotePostId">Platform-side post identifier when publishing succeeds.</param>
/// <param name="PublishedAtUtc">UTC timestamp when the platform confirmed the content.</param>
/// <param name="ErrorMessage">Provider-specific error message when the call fails.</param>
/// <param name="RawResponse">Optional raw response payload preserved for diagnostics and audit.</param>
public sealed record PlatformPublishResult(
    bool IsSuccess,
    string? RemotePostId,
    DateTime? PublishedAtUtc,
    string? ErrorMessage,
    string? RawResponse);

/// <summary>
/// Normalized outcome of platform credential validation or refresh.
/// </summary>
/// <param name="IsValid">Whether the credentials are valid for the next outbound action.</param>
/// <param name="WasRefreshed">Whether a refresh call was required before the credentials became valid.</param>
/// <param name="ExpiresAtUtc">The next known UTC expiration timestamp, if the provider exposes one.</param>
/// <param name="FailureReason">Human-readable explanation when the credentials remain invalid.</param>
public sealed record PlatformTokenValidationResult(
    bool IsValid,
    bool WasRefreshed,
    DateTime? ExpiresAtUtc,
    string? FailureReason);

/// <summary>
/// Normalized public profile payload returned when the system resolves provider-side account metadata.
/// </summary>
/// <param name="ExternalAccountId">Platform-side identifier of the connected account.</param>
/// <param name="DisplayName">Public display name of the remote account.</param>
/// <param name="Username">Optional public handle or username.</param>
/// <param name="AvatarUrl">Optional profile avatar URL.</param>
/// <param name="AccountType">Optional provider-specific account type.</param>
/// <param name="IsPrivateAccount">Whether the remote account is private.</param>
/// <param name="FollowersCount">Optional follower or subscriber count.</param>
public sealed record PlatformAccountProfile(
    string ExternalAccountId,
    string DisplayName,
    string? Username,
    string? AvatarUrl,
    string? AccountType,
    bool IsPrivateAccount,
    long? FollowersCount);

/// <summary>
/// Normalized outcome of exchanging an OAuth authorization code for long-lived account credentials.
/// </summary>
/// <param name="AccessToken">Raw provider access token that must be protected before persistence.</param>
/// <param name="RefreshToken">Raw provider refresh token when available.</param>
/// <param name="ExpiresAtUtc">UTC expiration timestamp for the access token.</param>
/// <param name="GrantedScopesCsv">Granted scopes stored as a comma-separated string.</param>
/// <param name="Profile">Resolved public profile payload of the connected account.</param>
public sealed record PlatformConnectionResult(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAtUtc,
    string GrantedScopesCsv,
    PlatformAccountProfile Profile);

/// <summary>
/// Normalized account-level insight snapshot returned by provider analytics endpoints.
/// </summary>
/// <param name="RecordedAtUtc">UTC timestamp when the provider metrics were observed.</param>
/// <param name="FollowersCount">Follower or subscriber count.</param>
/// <param name="FollowingCount">Following count when provided by the platform.</param>
/// <param name="TotalPostsCount">Total number of published items on the account.</param>
/// <param name="Reach">Optional reach metric.</param>
/// <param name="Impressions">Optional impressions metric.</param>
public sealed record PlatformAccountInsightSnapshot(
    DateTime RecordedAtUtc,
    long FollowersCount,
    long FollowingCount,
    long TotalPostsCount,
    long? Reach,
    long? Impressions);

/// <summary>
/// Normalized post-level analytics snapshot returned by provider analytics endpoints.
/// </summary>
/// <param name="RecordedAtUtc">UTC timestamp when the provider metrics were observed.</param>
/// <param name="Views">View count.</param>
/// <param name="Likes">Like or reaction count.</param>
/// <param name="Comments">Comment count.</param>
/// <param name="Shares">Share or repost count.</param>
/// <param name="Saves">Save or bookmark count.</param>
/// <param name="Reach">Optional reach metric.</param>
/// <param name="Impressions">Optional impressions metric.</param>
/// <param name="AverageWatchTime">Optional average watch time in seconds.</param>
/// <param name="CompletionRate">Optional completion-rate coefficient.</param>
public sealed record PlatformPostAnalyticsSnapshot(
    DateTime RecordedAtUtc,
    long Views,
    long Likes,
    long Comments,
    long Shares,
    long Saves,
    long? Reach,
    long? Impressions,
    double? AverageWatchTime,
    double? CompletionRate);
