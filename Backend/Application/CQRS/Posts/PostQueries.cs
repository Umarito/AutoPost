using Application.Common;
using Application.DTOs.Post;
using Application.DTOs.PublishingJob;
using MediatR;

namespace Application.CQRS.Posts;

/// <summary>
/// Retrieves a filtered and paginated list of posts for the current workspace.
/// </summary>
/// <param name="Filter">Optional filter set for status, platform, dates and search.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetPostsPagedQuery(PostFilterRequest Filter, PagedRequest Pagination) : IRequest<Result<PagedResult<PostSummaryDto>>>;

/// <summary>
/// Retrieves one post with its full detail projection.
/// </summary>
/// <param name="PostId">Post identifier that should be loaded.</param>
public sealed record GetPostByIdQuery(Guid PostId) : IRequest<Result<PostDetailDto>>;

/// <summary>
/// Retrieves lightweight post events for the content calendar view.
/// </summary>
/// <param name="From">Inclusive UTC start of the requested calendar window.</param>
/// <param name="To">Inclusive UTC end of the requested calendar window.</param>
public sealed record GetPostCalendarQuery(DateTime From, DateTime To) : IRequest<Result<IReadOnlyList<PostCalendarDto>>>;

/// <summary>
/// Retrieves aggregated operational statistics for posts in a selected time window.
/// </summary>
/// <param name="From">Optional inclusive UTC start date.</param>
/// <param name="To">Optional inclusive UTC end date.</param>
public sealed record GetPostsStatisticsQuery(DateTime? From, DateTime? To) : IRequest<Result<PostStatisticsDto>>;

/// <summary>
/// Retrieves the current publishing status of a specific target within a post.
/// </summary>
/// <param name="PostTargetId">Target whose status should be returned.</param>
public sealed record GetPostTargetStatusQuery(Guid PostTargetId) : IRequest<Result<PostTargetDto>>;

/// <summary>
/// Retrieves the paginated publishing history for all targets of a post.
/// </summary>
/// <param name="PostId">Post whose publishing attempts should be listed.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetPublishingHistoryQuery(Guid PostId, PagedRequest Pagination) : IRequest<Result<PagedResult<PublishingJobDto>>>;

/// <summary>
/// Retrieves recent failed publication attempts to support operational monitoring.
/// </summary>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetFailedPublicationsQuery(PagedRequest Pagination) : IRequest<Result<PagedResult<PublishingJobDto>>>;
