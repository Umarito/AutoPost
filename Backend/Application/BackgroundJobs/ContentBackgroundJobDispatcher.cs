using Application.CQRS.Analytics;
using Application.CQRS.Posts;
using Application.CQRS.SocialAccounts;
using Application.CQRS.Videos;
using MediatR;

namespace Application.BackgroundJobs;

/// <summary>
/// Bridges Hangfire job execution with MediatR-based content commands.
/// </summary>
public sealed class ContentBackgroundJobDispatcher
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes the dispatcher with the mediator used to re-enter the CQRS pipeline.
    /// </summary>
    /// <param name="mediator">Mediator used to dispatch commands from background jobs.</param>
    public ContentBackgroundJobDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Replays the video-processing command from a durable background job.
    /// </summary>
    /// <param name="videoId">Video that should be processed.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task ProcessVideoAsync(Guid videoId)
        => _mediator.Send(new ProcessVideoCommand(videoId));

    /// <summary>
    /// Replays the thumbnail-generation command from a durable background job.
    /// </summary>
    /// <param name="videoId">Video whose thumbnail should be generated.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task GenerateThumbnailAsync(Guid videoId)
        => _mediator.Send(new GenerateVideoThumbnailCommand(videoId));

    /// <summary>
    /// Replays the post publication command from a durable background job.
    /// </summary>
    /// <param name="postId">Post that should be published.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task PublishPostAsync(Guid postId)
        => _mediator.Send(new PublishPostCommand(postId));

    /// <summary>
    /// Replays the single-target publication command from a durable background job.
    /// </summary>
    /// <param name="postTargetId">Target that should be published.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task PublishTargetAsync(Guid postTargetId)
        => _mediator.Send(new PublishToTargetCommand(postTargetId));

    /// <summary>
    /// Replays the account-insight collection command from a durable background job.
    /// </summary>
    /// <param name="socialAccountId">Connected account whose metrics should be collected.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task CollectAccountInsightAsync(Guid socialAccountId)
        => _mediator.Send(new CollectAccountInsightCommand(socialAccountId));

    /// <summary>
    /// Replays the post-analytics snapshot collection command from a durable background job.
    /// </summary>
    /// <param name="postTargetId">Published target whose metrics should be collected.</param>
    /// <returns>A task that completes when the command has been executed.</returns>
    public Task CollectPostSnapshotAsync(Guid postTargetId)
        => _mediator.Send(new CollectPostSnapshotCommand(postTargetId));
}
