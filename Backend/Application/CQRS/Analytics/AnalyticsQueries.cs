using Application.Common;
using Application.DTOs.Analytics;
using MediatR;

namespace Application.CQRS.Analytics;

/// <summary>
/// Retrieves cross-platform analytics for a single post.
/// </summary>
/// <param name="PostId">Post whose analytics should be returned.</param>
public sealed record GetPostAnalyticsQuery(Guid PostId) : IRequest<Result<PostAnalyticsDto>>;

/// <summary>
/// Retrieves a workspace dashboard analytics summary.
/// </summary>
/// <param name="From">Optional inclusive UTC start date for the summary window.</param>
/// <param name="To">Optional inclusive UTC end date for the summary window.</param>
public sealed record GetDashboardSummaryQuery(DateTime? From, DateTime? To) : IRequest<Result<DashboardSummaryDto>>;
