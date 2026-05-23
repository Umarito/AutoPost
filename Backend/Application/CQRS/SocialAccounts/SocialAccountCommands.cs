using Application.Common;
using Application.DTOs.Analytics;
using Application.DTOs.SocialAccount;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.SocialAccounts;

/// <summary>
/// Completes the OAuth callback flow for a platform and persists the connected social account.
/// </summary>
/// <param name="Platform">Platform that issued the callback.</param>
/// <param name="AuthorizationCode">Provider authorization code returned by the OAuth redirect.</param>
/// <param name="State">CSRF protection state value that must match the protected state cookie.</param>
/// <param name="RedirectUri">Redirect URI used during authorization and token exchange.</param>
public sealed record HandleOAuthCallbackCommand(
    Platform Platform,
    string AuthorizationCode,
    string State,
    string RedirectUri) : IRequest<Result<SocialAccountDto>>;

/// <summary>
/// Disconnects a previously connected social account from the workspace.
/// </summary>
/// <param name="SocialAccountId">Connected account that should be removed.</param>
public sealed record DisconnectSocialAccountCommand(Guid SocialAccountId) : IRequest<Result>;

/// <summary>
/// Forces a validity check and token refresh for a connected social account.
/// </summary>
/// <param name="SocialAccountId">Connected account whose credentials should be refreshed if needed.</param>
public sealed record EnsureTokenValidCommand(Guid SocialAccountId) : IRequest<Result>;

/// <summary>
/// Refreshes public metadata for a connected social account such as display name and follower count.
/// </summary>
/// <param name="SocialAccountId">Connected account whose platform metadata should be refreshed.</param>
public sealed record RefreshSocialAccountMetaCommand(Guid SocialAccountId) : IRequest<Result<SocialAccountDto>>;

/// <summary>
/// Collects a new account-level insight snapshot for a connected social account.
/// </summary>
/// <param name="SocialAccountId">Connected account whose metrics should be sampled.</param>
public sealed record CollectAccountInsightCommand(Guid SocialAccountId) : IRequest<Result>;
