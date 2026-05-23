using Application.Common;
using Application.DTOs.Post;
using MediatR;

namespace Application.CQRS.Posts;

/// <summary>
/// Creates a new post aggregate and its initial publishing targets.
/// </summary>
/// <param name="Request">Post composition payload containing content, schedule and targets.</param>
public sealed record CreatePostCommand(CreatePostRequest Request) : IRequest<Result<PostDetailDto>>;

/// <summary>
/// Updates an existing draft or scheduled post.
/// </summary>
/// <param name="PostId">Post that should be updated.</param>
/// <param name="Request">Patch-style update payload.</param>
public sealed record UpdatePostCommand(Guid PostId, UpdatePostRequest Request) : IRequest<Result<PostDetailDto>>;

/// <summary>
/// Cancels a scheduled post before it starts publishing.
/// </summary>
/// <param name="PostId">Post that should be cancelled.</param>
public sealed record CancelPostCommand(Guid PostId) : IRequest<Result>;

/// <summary>
/// Permanently deletes a post that is still eligible for removal.
/// </summary>
/// <param name="PostId">Post that should be deleted.</param>
public sealed record DeletePostCommand(Guid PostId) : IRequest<Result>;

/// <summary>
/// Changes the planned publication time of a scheduled post.
/// </summary>
/// <param name="PostId">Post that should be rescheduled.</param>
/// <param name="ScheduledAtUtc">New UTC publication timestamp.</param>
/// <param name="TimeZoneId">User timezone that should be preserved for display and UX.</param>
public sealed record ReschedulePostCommand(Guid PostId, DateTime ScheduledAtUtc, string TimeZoneId) : IRequest<Result<PostDetailDto>>;

/// <summary>
/// Starts the publication workflow for a post immediately.
/// </summary>
/// <param name="PostId">Post that should be published now.</param>
public sealed record PublishPostCommand(Guid PostId) : IRequest<Result>;

/// <summary>
/// Publishes a single post target to its connected platform.
/// </summary>
/// <param name="PostTargetId">Target that should be published.</param>
public sealed record PublishToTargetCommand(Guid PostTargetId) : IRequest<Result>;

/// <summary>
/// Retries a failed publication target.
/// </summary>
/// <param name="PostTargetId">Target whose failed publication should be retried.</param>
public sealed record RetryFailedTargetCommand(Guid PostTargetId) : IRequest<Result>;

/// <summary>
/// Records the final outcome of a publication attempt for a target.
/// </summary>
/// <param name="PostTargetId">Target whose status should be updated.</param>
/// <param name="IsSuccess">Whether the publication succeeded.</param>
/// <param name="ExternalPostUrl">Platform URL of the published post when available.</param>
/// <param name="ErrorMessage">Failure description when the publication fails.</param>
/// <param name="RawResponse">Optional raw provider response used for diagnostics.</param>
public sealed record RecordPublishResultCommand(
    Guid PostTargetId,
    bool IsSuccess,
    string? ExternalPostUrl,
    string? ErrorMessage,
    string? RawResponse) : IRequest<Result>;

/// <summary>
/// Creates a publishing job audit record before an outbound platform request is executed.
/// </summary>
/// <param name="PostTargetId">Target this attempt belongs to.</param>
/// <param name="AttemptNumber">Sequence number of the publishing attempt.</param>
/// <param name="SchedulerJobId">Optional background job identifier used for correlation.</param>
public sealed record CreatePublishingJobCommand(Guid PostTargetId, int AttemptNumber, string? SchedulerJobId) : IRequest<Result>;

/// <summary>
/// Updates a previously created publishing job with its final outcome and diagnostics.
/// </summary>
/// <param name="PublishingJobId">Publishing job that should be updated.</param>
/// <param name="Outcome">Normalized outcome value as string.</param>
/// <param name="ErrorDetails">Technical error details when the attempt fails.</param>
/// <param name="RawApiResponse">Optional raw provider response for debugging.</param>
public sealed record UpdatePublishingJobCommand(
    Guid PublishingJobId,
    string Outcome,
    string? ErrorDetails,
    string? RawApiResponse) : IRequest<Result>;
