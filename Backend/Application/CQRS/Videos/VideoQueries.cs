using Application.Common;
using Application.DTOs.Post;
using Application.DTOs.Video;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Videos;

/// <summary>
/// Retrieves a paginated list of videos from the workspace media library.
/// </summary>
/// <param name="Status">Optional status filter for processing state.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetVideosPagedQuery(VideoStatus? Status, PagedRequest Pagination) : IRequest<Result<PagedResult<VideoSummaryDto>>>;

/// <summary>
/// Retrieves one video with its full metadata and playback information.
/// </summary>
/// <param name="VideoId">Video that should be loaded.</param>
public sealed record GetVideoByIdQuery(Guid VideoId) : IRequest<Result<VideoDetailDto>>;

/// <summary>
/// Validates whether a video is compatible with the selected publishing platforms.
/// </summary>
/// <param name="VideoId">Video that should be validated.</param>
/// <param name="Platforms">Target platforms that the media file should be checked against.</param>
public sealed record ValidateVideoForPlatformsQuery(Guid VideoId, IReadOnlyList<Platform> Platforms) : IRequest<Result<IReadOnlyList<PlatformValidationDto>>>;
