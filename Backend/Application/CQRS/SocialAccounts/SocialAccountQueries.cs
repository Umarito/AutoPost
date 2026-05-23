using Application.Common;
using Application.DTOs.Analytics;
using Application.DTOs.SocialAccount;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.SocialAccounts;

/// <summary>
/// Generates the platform-specific OAuth authorization URL used to start account connection.
/// </summary>
/// <param name="Platform">Platform the user wants to connect.</param>
/// <param name="RedirectUri">Frontend callback URI that should receive the provider response.</param>
public sealed record GetOAuthUrlQuery(Platform Platform, string RedirectUri) : IRequest<Result<OAuthUrlDto>>;

/// <summary>
/// Retrieves all connected social accounts for the current workspace.
/// </summary>
public sealed record GetSocialAccountsQuery() : IRequest<Result<IReadOnlyList<SocialAccountDto>>>;

/// <summary>
/// Retrieves follower and reach growth snapshots for a connected social account.
/// </summary>
/// <param name="SocialAccountId">Connected account whose growth history should be returned.</param>
/// <param name="From">Inclusive UTC start of the insight time window.</param>
/// <param name="To">Inclusive UTC end of the insight time window.</param>
public sealed record GetAccountGrowthQuery(Guid SocialAccountId, DateTime From, DateTime To) : IRequest<Result<IReadOnlyList<InsightDto>>>;
