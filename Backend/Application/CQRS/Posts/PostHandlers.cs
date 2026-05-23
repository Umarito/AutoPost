using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.BackgroundJobs;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Post;
using Application.DTOs.PublishingJob;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Posts;

/// <summary>
/// Creates a post aggregate, validates the selected media and targets, and schedules durable publication.
/// </summary>
public sealed class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, Result<PostDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IPostRepository _postRepository;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreatePostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-creation handler.
    /// </summary>
    public CreatePostCommandHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        ISocialAccountRepository socialAccountRepository,
        IPostRepository postRepository,
        IPostTargetRepository postTargetRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<CreatePostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _socialAccountRepository = socialAccountRepository;
        _postRepository = postRepository;
        _postTargetRepository = postTargetRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostDetailDto>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            var video = await _videoRepository.GetByIdAsync(request.Request.VideoId, cancellationToken);
            if (video is null || video.WorkspaceId != _currentUserContext.WorkspaceId)
            {
                return ContentGuard.NotFound<PostDetailDto>("Video");
            }

            if (video.Status != VideoStatus.Ready)
            {
                return Result<PostDetailDto>.Fail("The selected video is not ready for publication yet.", ErrorCode.Conflict);
            }

            var requestedAccountIds = request.Request.TargetAccountIds
                .Distinct()
                .ToArray();

            var workspaceAccounts = await _socialAccountRepository.GetByWorkspaceIdAsync(_currentUserContext.WorkspaceId, cancellationToken);
            var selectedAccounts = workspaceAccounts
                .Where(account => requestedAccountIds.Contains(account.Id))
                .ToArray();

            if (selectedAccounts.Length != requestedAccountIds.Length)
            {
                return Result<PostDetailDto>.Fail("One or more target social accounts were not found inside the current workspace.", ErrorCode.Validation);
            }

            if (selectedAccounts.Any(account => account.Status != SocialAccountStatus.Active))
            {
                return Result<PostDetailDto>.Fail("All selected social accounts must be active before a post can be scheduled.", ErrorCode.Conflict);
            }

            var utcNow = DateTime.UtcNow;
            var content = PostContent.Create(
                request.Request.Title.Trim(),
                request.Request.Description,
                request.Request.Tags,
                request.Request.Visibility,
                request.Request.CustomThumbnailUrl,
                request.Request.PlatformSettingsJson);

            var schedule = Schedule.Create(
                request.Request.ScheduledAt <= utcNow ? utcNow : request.Request.ScheduledAt,
                request.Request.TimeZoneId);

            var post = Post.Create(
                _currentUserContext.WorkspaceId,
                _currentUserContext.UserId,
                video.Id,
                content,
                schedule,
                utcNow);

            await using (var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                await _postRepository.AddAsync(post, cancellationToken);

                foreach (var account in selectedAccounts)
                {
                    await _postTargetRepository.AddAsync(
                        PostTarget.Create(post.Id, account.Id, account.Platform),
                        cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            var delay = schedule.ScheduledAt <= utcNow
                ? TimeSpan.Zero
                : schedule.ScheduledAt - utcNow;

            var schedulerJobId = delay == TimeSpan.Zero
                ? _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.PublishPostAsync(post.Id),
                    queue: "default")
                : _backgroundJobScheduler.Schedule<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.PublishPostAsync(post.Id),
                    delay,
                    queue: "default");

            post.Reschedule(schedule.WithSchedulerJobId(schedulerJobId), utcNow);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);

            var reloaded = await _postRepository.GetByIdWithTargetsAsync(post.Id, cancellationToken);
            return reloaded is null
                ? ContentGuard.NotFound<PostDetailDto>("Post")
                : Result<PostDetailDto>.Ok(_mapper.Map<PostDetailDto>(reloaded));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while creating a post in workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<PostDetailDto>.Fail("An unexpected post-creation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Updates post content and schedule for mutable posts while keeping durable scheduler correlation in sync.
/// </summary>
public sealed class UpdatePostCommandHandler : IRequestHandler<UpdatePostCommand, Result<PostDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdatePostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-update handler.
    /// </summary>
    public UpdatePostCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<UpdatePostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostDetailDto>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdWithTargetsAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PostDetailDto>("Post");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            if (post.Status is not PostStatus.Draft and not PostStatus.Scheduled)
            {
                return Result<PostDetailDto>.Fail("Only draft or scheduled posts can be updated.", ErrorCode.Conflict);
            }

            var utcNow = DateTime.UtcNow;
            post.UpdateContent(
                post.Content.Update(
                    request.Request.Title?.Trim(),
                    request.Request.Description,
                    request.Request.Tags,
                    request.Request.Visibility),
                utcNow);

            if (request.Request.ScheduledAt.HasValue || !string.IsNullOrWhiteSpace(request.Request.TimeZoneId))
            {
                if (!string.IsNullOrWhiteSpace(post.Schedule.SchedulerJobId))
                {
                    _backgroundJobScheduler.Delete(post.Schedule.SchedulerJobId);
                }

                var updatedSchedule = post.Schedule.Reschedule(
                    request.Request.ScheduledAt ?? post.Schedule.ScheduledAt,
                    request.Request.TimeZoneId ?? post.Schedule.TimeZoneId);

                var delay = updatedSchedule.ScheduledAt <= utcNow
                    ? TimeSpan.Zero
                    : updatedSchedule.ScheduledAt - utcNow;

                var schedulerJobId = delay == TimeSpan.Zero
                    ? _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                        dispatcher => dispatcher.PublishPostAsync(post.Id),
                        queue: "default")
                    : _backgroundJobScheduler.Schedule<ContentBackgroundJobDispatcher>(
                        dispatcher => dispatcher.PublishPostAsync(post.Id),
                        delay,
                        queue: "default");

                post.Reschedule(updatedSchedule.WithSchedulerJobId(schedulerJobId), utcNow);
            }

            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);

            return Result<PostDetailDto>.Ok(_mapper.Map<PostDetailDto>(post));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while updating post {PostId}.", request.PostId);
            return Result<PostDetailDto>.Fail("An unexpected post-update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Cancels a scheduled post before any outbound publication starts.
/// </summary>
public sealed class CancelPostCommandHandler : IRequestHandler<CancelPostCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CancelPostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-cancellation handler.
    /// </summary>
    public CancelPostCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<CancelPostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(CancelPostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result.Fail(access.Error!, access.Code!.Value);
            }

            if (post.Status is not PostStatus.Scheduled and not PostStatus.Draft)
            {
                return Result.Fail("Only draft or scheduled posts can be cancelled.", ErrorCode.Conflict);
            }

            if (!string.IsNullOrWhiteSpace(post.Schedule.SchedulerJobId))
            {
                _backgroundJobScheduler.Delete(post.Schedule.SchedulerJobId);
            }

            post.Cancel(DateTime.UtcNow);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while cancelling post {PostId}.", request.PostId);
            return Result.Fail("An unexpected post-cancellation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Deletes a post that is still safe to remove without corrupting publication history.
/// </summary>
public sealed class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeletePostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-delete handler.
    /// </summary>
    public DeletePostCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<DeletePostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result.Fail(access.Error!, access.Code!.Value);
            }

            if (post.Status is PostStatus.Publishing or PostStatus.Published or PostStatus.PartiallyFailed)
            {
                return Result.Fail("Published or in-flight posts cannot be deleted.", ErrorCode.Conflict);
            }

            if (!string.IsNullOrWhiteSpace(post.Schedule.SchedulerJobId))
            {
                _backgroundJobScheduler.Delete(post.Schedule.SchedulerJobId);
            }

            _postRepository.Remove(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while deleting post {PostId}.", request.PostId);
            return Result.Fail("An unexpected post-deletion error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Changes the publication time of a scheduled or draft post and reschedules the durable Hangfire trigger.
/// </summary>
public sealed class ReschedulePostCommandHandler : IRequestHandler<ReschedulePostCommand, Result<PostDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<ReschedulePostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-rescheduling handler.
    /// </summary>
    public ReschedulePostCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<ReschedulePostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostDetailDto>> Handle(ReschedulePostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdWithTargetsAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PostDetailDto>("Post");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            if (post.Status is not PostStatus.Scheduled and not PostStatus.Draft)
            {
                return Result<PostDetailDto>.Fail("Only draft or scheduled posts can be rescheduled.", ErrorCode.Conflict);
            }

            if (!string.IsNullOrWhiteSpace(post.Schedule.SchedulerJobId))
            {
                _backgroundJobScheduler.Delete(post.Schedule.SchedulerJobId);
            }

            var utcNow = DateTime.UtcNow;
            var schedule = post.Schedule.Reschedule(request.ScheduledAtUtc, request.TimeZoneId);
            var delay = schedule.ScheduledAt <= utcNow
                ? TimeSpan.Zero
                : schedule.ScheduledAt - utcNow;

            var schedulerJobId = delay == TimeSpan.Zero
                ? _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.PublishPostAsync(post.Id),
                    queue: "default")
                : _backgroundJobScheduler.Schedule<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.PublishPostAsync(post.Id),
                    delay,
                    queue: "default");

            post.Reschedule(schedule.WithSchedulerJobId(schedulerJobId), utcNow);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);

            return Result<PostDetailDto>.Ok(_mapper.Map<PostDetailDto>(post));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while rescheduling post {PostId}.", request.PostId);
            return Result<PostDetailDto>.Fail("An unexpected post-rescheduling error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Starts the multi-target publication workflow and fans out one durable background job per target.
/// </summary>
public sealed class PublishPostCommandHandler : IRequestHandler<PublishPostCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PublishPostCommandHandler> _logger;

    /// <summary>
    /// Initializes the post-publication orchestrator.
    /// </summary>
    public PublishPostCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<PublishPostCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(PublishPostCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdWithTargetsAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    post.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            if (post.Status is PostStatus.Cancelled or PostStatus.Published)
            {
                return Result.Fail("The post is not eligible for publication.", ErrorCode.Conflict);
            }

            if (post.VideoId.HasValue && post.Video?.Status != VideoStatus.Ready)
            {
                return Result.Fail("The attached video is not ready for publication.", ErrorCode.Conflict);
            }

            var targetsToPublish = post.Targets
                .Where(target => target.Status is TargetStatus.Pending or TargetStatus.Failed or TargetStatus.Retrying)
                .ToArray();

            if (targetsToPublish.Length == 0)
            {
                return Result.Fail("No pending publication targets were found for the post.", ErrorCode.Conflict);
            }

            post.MarkPublishing(DateTime.UtcNow);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var target in targetsToPublish)
            {
                _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.PublishTargetAsync(target.Id),
                    queue: "default");
            }

            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while starting publication for post {PostId}.", request.PostId);
            return Result.Fail("An unexpected publication-orchestration error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Executes publication for one target, records the attempt and applies the final platform outcome.
/// </summary>
public sealed class PublishToTargetCommandHandler : IRequestHandler<PublishToTargetCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPublishingJobRepository _publishingJobRepository;
    private readonly IPlatformPublisherFactory _platformPublisherFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PublishToTargetCommandHandler> _logger;

    /// <summary>
    /// Initializes the single-target publication handler.
    /// </summary>
    public PublishToTargetCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPublishingJobRepository publishingJobRepository,
        IPlatformPublisherFactory platformPublisherFactory,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<PublishToTargetCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _publishingJobRepository = publishingJobRepository;
        _platformPublisherFactory = platformPublisherFactory;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(PublishToTargetCommand request, CancellationToken cancellationToken)
    {
        PublishingJob? publishingJob = null;
        PostTarget? target = null;
        Post? post = null;

        try
        {
            target = await _postTargetRepository.GetByIdAsync(request.PostTargetId, cancellationToken);
            if (target is null)
            {
                return ContentGuard.NotFound("Post target");
            }

            post = await _postRepository.GetByIdWithTargetsAsync(target.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    post.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            if (target.Status == TargetStatus.Published)
            {
                return Result.Ok();
            }

            var attemptNumber = (target.Result?.AttemptCount ?? 0) + 1;
            var startedAtUtc = DateTime.UtcNow;

            await using (var beginTransaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                target.IncrementAttempt();
                target.MarkPublishing();
                post.MarkPublishing(startedAtUtc);

                publishingJob = PublishingJob.Start(target.Id, attemptNumber, null, startedAtUtc);

                _postTargetRepository.Update(target);
                _postRepository.Update(post);
                await _publishingJobRepository.AddAsync(publishingJob, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await beginTransaction.CommitAsync(cancellationToken);
            }

            var publisher = _platformPublisherFactory.Create(target.Platform);
            var publishRequest = new PlatformPublishRequest(
                post.Id,
                target.Id,
                post.WorkspaceId,
                target.Platform,
                target.SocialAccountId,
                PostWorkflow.ComposeCaption(post.Content),
                post.Video?.CdnUrl,
                post.Schedule.ScheduledAt);

            var publishResult = await publisher.PublishAsync(publishRequest, cancellationToken);
            var completedAtUtc = DateTime.UtcNow;

            await using (var finalizeTransaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                if (publishResult.IsSuccess)
                {
                    target.MarkPublished(
                        publishResult.RemotePostId,
                        null,
                        publishResult.PublishedAtUtc ?? completedAtUtc);

                    publishingJob!.Complete(JobOutcome.Succeeded, completedAtUtc, null, publishResult.RawResponse);
                }
                else
                {
                    target.MarkFailed(null, publishResult.ErrorMessage ?? "The remote platform rejected the publication attempt.");
                    publishingJob!.Complete(
                        JobOutcome.Failed,
                        completedAtUtc,
                        publishResult.ErrorMessage,
                        publishResult.RawResponse);
                }

                post.ApplyPublicationOutcome(
                    PostWorkflow.CalculateAggregateStatus(post.Targets),
                    PostWorkflow.CalculateCompletedAt(post.Targets, completedAtUtc),
                    completedAtUtc);

                _postTargetRepository.Update(target);
                _publishingJobRepository.Update(publishingJob);
                _postRepository.Update(post);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await finalizeTransaction.CommitAsync(cancellationToken);
            }

            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);

            if (publishResult.IsSuccess)
            {
                _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.CollectPostSnapshotAsync(target.Id),
                    queue: "default");

                return Result.Ok();
            }

            return Result.Fail(publishResult.ErrorMessage ?? "The remote platform rejected the publication attempt.", ErrorCode.ExternalApi);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while publishing post target {PostTargetId}.", request.PostTargetId);

            if (target is not null && post is not null && publishingJob is not null)
            {
                try
                {
                    await using var rollbackTransaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
                    target.MarkFailed(null, "The publishing attempt ended with an unexpected infrastructure error.");
                    publishingJob.Complete(JobOutcome.Failed, DateTime.UtcNow, exception.Message, null);
                    post.ApplyPublicationOutcome(
                        PostWorkflow.CalculateAggregateStatus(post.Targets),
                        PostWorkflow.CalculateCompletedAt(post.Targets, DateTime.UtcNow),
                        DateTime.UtcNow);

                    _postTargetRepository.Update(target);
                    _publishingJobRepository.Update(publishingJob);
                    _postRepository.Update(post);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await rollbackTransaction.CommitAsync(cancellationToken);
                    await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
                }
                catch (Exception fallbackException)
                {
                    _logger.LogError(fallbackException, "Failed to persist fallback failure state for post target {PostTargetId}.", request.PostTargetId);
                }
            }

            return Result.Fail("An unexpected target-publication error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Requeues a failed target for another durable publication attempt.
/// </summary>
public sealed class RetryFailedTargetCommandHandler : IRequestHandler<RetryFailedTargetCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RetryFailedTargetCommandHandler> _logger;

    /// <summary>
    /// Initializes the target-retry handler.
    /// </summary>
    public RetryFailedTargetCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<RetryFailedTargetCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(RetryFailedTargetCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var target = await _postTargetRepository.GetByIdAsync(request.PostTargetId, cancellationToken);
            if (target is null)
            {
                return ContentGuard.NotFound("Post target");
            }

            var post = await _postRepository.GetByIdAsync(target.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result.Fail(access.Error!, access.Code!.Value);
            }

            if (target.Status != TargetStatus.Failed)
            {
                return Result.Fail("Only failed publication targets can be retried.", ErrorCode.Conflict);
            }

            target.MarkRetrying();
            post.MarkPublishing(DateTime.UtcNow);
            _postTargetRepository.Update(target);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                dispatcher => dispatcher.PublishTargetAsync(target.Id),
                queue: "default");

            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while retrying post target {PostTargetId}.", request.PostTargetId);
            return Result.Fail("An unexpected publication-retry error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Applies a normalized publish outcome that may have been produced by an external worker or retry orchestrator.
/// </summary>
public sealed class RecordPublishResultCommandHandler : IRequestHandler<RecordPublishResultCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RecordPublishResultCommandHandler> _logger;

    /// <summary>
    /// Initializes the publish-result handler.
    /// </summary>
    public RecordPublishResultCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        ICacheService cacheService,
        ILogger<RecordPublishResultCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(RecordPublishResultCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var target = await _postTargetRepository.GetByIdAsync(request.PostTargetId, cancellationToken);
            if (target is null)
            {
                return ContentGuard.NotFound("Post target");
            }

            var post = await _postRepository.GetByIdWithTargetsAsync(target.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    post.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            var utcNow = DateTime.UtcNow;
            if (request.IsSuccess)
            {
                target.MarkPublished(
                    target.Result?.ExternalPostId,
                    request.ExternalPostUrl,
                    utcNow);
            }
            else
            {
                target.MarkFailed(null, request.ErrorMessage ?? "The publication attempt failed.");
            }

            post.ApplyPublicationOutcome(
                PostWorkflow.CalculateAggregateStatus(post.Targets),
                PostWorkflow.CalculateCompletedAt(post.Targets, utcNow),
                utcNow);

            _postTargetRepository.Update(target);
            _postRepository.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PostWorkflow.BumpWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);

            if (request.IsSuccess)
            {
                _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                    dispatcher => dispatcher.CollectPostSnapshotAsync(target.Id),
                    queue: "default");
            }

            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while recording a publication outcome for target {PostTargetId}.", request.PostTargetId);
            return Result.Fail("An unexpected publish-result persistence error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Creates a standalone publishing-job audit row for external orchestrators that need an explicit attempt record.
/// </summary>
public sealed class CreatePublishingJobCommandHandler : IRequestHandler<CreatePublishingJobCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPublishingJobRepository _publishingJobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreatePublishingJobCommandHandler> _logger;

    /// <summary>
    /// Initializes the publishing-job creation handler.
    /// </summary>
    public CreatePublishingJobCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPublishingJobRepository publishingJobRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreatePublishingJobCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _publishingJobRepository = publishingJobRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(CreatePublishingJobCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var target = await _postTargetRepository.GetByIdAsync(request.PostTargetId, cancellationToken);
            if (target is null)
            {
                return ContentGuard.NotFound("Post target");
            }

            var post = await _postRepository.GetByIdAsync(target.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound("Post");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    post.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            await _publishingJobRepository.AddAsync(
                PublishingJob.Start(target.Id, request.AttemptNumber, request.SchedulerJobId, DateTime.UtcNow),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while creating publishing job for target {PostTargetId}.", request.PostTargetId);
            return Result.Fail("An unexpected publishing-job creation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Updates the final outcome of an existing publishing-job audit entry.
/// </summary>
public sealed class UpdatePublishingJobCommandHandler : IRequestHandler<UpdatePublishingJobCommand, Result>
{
    private readonly IPublishingJobRepository _publishingJobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdatePublishingJobCommandHandler> _logger;

    /// <summary>
    /// Initializes the publishing-job update handler.
    /// </summary>
    public UpdatePublishingJobCommandHandler(
        IPublishingJobRepository publishingJobRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdatePublishingJobCommandHandler> logger)
    {
        _publishingJobRepository = publishingJobRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(UpdatePublishingJobCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var publishingJob = await _publishingJobRepository.GetByIdAsync(request.PublishingJobId, cancellationToken);
            if (publishingJob is null)
            {
                return ContentGuard.NotFound("Publishing job");
            }

            if (!Enum.TryParse<JobOutcome>(request.Outcome, true, out var outcome))
            {
                return Result.Fail("Publishing job outcome value is invalid.", ErrorCode.Validation);
            }

            publishingJob.Complete(outcome, DateTime.UtcNow, request.ErrorDetails, request.RawApiResponse);
            _publishingJobRepository.Update(publishingJob);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while updating publishing job {PublishingJobId}.", request.PublishingJobId);
            return Result.Fail("An unexpected publishing-job update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns a filtered, paginated post list backed by Redis cache-aside for repeated workspace reads.
/// </summary>
public sealed class GetPostsPagedQueryHandler : IRequestHandler<GetPostsPagedQuery, Result<PagedResult<PostSummaryDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetPostsPagedQueryHandler> _logger;

    /// <summary>
    /// Initializes the paged-post query handler.
    /// </summary>
    public GetPostsPagedQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetPostsPagedQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<PostSummaryDto>>> Handle(GetPostsPagedQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PagedResult<PostSummaryDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cacheStamp = await PostWorkflow.GetWorkspaceCacheStampAsync(_cacheService, _currentUserContext.WorkspaceId, cancellationToken);
            var cacheKey = PostWorkflow.BuildPostsCacheKey(_currentUserContext.WorkspaceId, request, cacheStamp);
            var cached = await _cacheService.GetAsync<PagedResult<PostSummaryDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<PagedResult<PostSummaryDto>>.Ok(cached);
            }

            var statusFilter = PostWorkflow.TryParsePostStatus(request.Filter.Status);
            var platformFilter = PostWorkflow.TryParsePlatform(request.Filter.Platform);

            var posts = await _postRepository.GetByWorkspaceIdAsync(
                _currentUserContext.WorkspaceId,
                statusFilter,
                platformFilter,
                request.Filter.From,
                request.Filter.To,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.Filter.Search))
            {
                posts = posts
                    .Where(post =>
                        (post.Content.Title?.Contains(request.Filter.Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (post.Content.Description?.Contains(request.Filter.Search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToArray();
            }

            var page = Math.Max(1, request.Pagination.Page);
            var pageSize = Math.Max(1, request.Pagination.PageSize);
            var items = posts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(_mapper.Map<PostSummaryDto>)
                .ToArray();

            var result = new PagedResult<PostSummaryDto>(items, posts.Count, page, pageSize);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<PagedResult<PostSummaryDto>>.Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading posts for workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<PagedResult<PostSummaryDto>>.Fail("An unexpected post-list lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns a single post detail after enforcing workspace ownership against persisted memberships.
/// </summary>
public sealed class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, Result<PostDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetPostByIdQueryHandler> _logger;

    /// <summary>
    /// Initializes the post-detail query handler.
    /// </summary>
    public GetPostByIdQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetPostByIdQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostDetailDto>> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdWithTargetsAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PostDetailDto>("Post");
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            var cacheStamp = await PostWorkflow.GetWorkspaceCacheStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            var cacheKey = PostWorkflow.BuildPostDetailCacheKey(post.Id, cacheStamp);
            var cached = await _cacheService.GetAsync<PostDetailDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<PostDetailDto>.Ok(cached);
            }

            var dto = _mapper.Map<PostDetailDto>(post);
            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<PostDetailDto>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading post {PostId}.", request.PostId);
            return Result<PostDetailDto>.Fail("An unexpected post lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns lightweight calendar items for the current workspace content calendar.
/// </summary>
public sealed class GetPostCalendarQueryHandler : IRequestHandler<GetPostCalendarQuery, Result<IReadOnlyList<PostCalendarDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetPostCalendarQueryHandler> _logger;

    /// <summary>
    /// Initializes the calendar query handler.
    /// </summary>
    public GetPostCalendarQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetPostCalendarQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PostCalendarDto>>> Handle(GetPostCalendarQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<PostCalendarDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cacheStamp = await PostWorkflow.GetWorkspaceCacheStampAsync(_cacheService, _currentUserContext.WorkspaceId, cancellationToken);
            var cacheKey = PostWorkflow.BuildCalendarCacheKey(_currentUserContext.WorkspaceId, request.From, request.To, cacheStamp);
            var cached = await _cacheService.GetAsync<IReadOnlyList<PostCalendarDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<PostCalendarDto>>.Ok(cached);
            }

            var posts = await _postRepository.GetByWorkspaceIdAsync(
                _currentUserContext.WorkspaceId,
                fromDate: request.From,
                toDate: request.To,
                ct: cancellationToken);

            var dto = posts
                .Where(post => post.Schedule.ScheduledAt >= request.From && post.Schedule.ScheduledAt <= request.To)
                .Select(_mapper.Map<PostCalendarDto>)
                .ToArray();

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<IReadOnlyList<PostCalendarDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading calendar posts for workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<IReadOnlyList<PostCalendarDto>>.Fail("An unexpected calendar lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Aggregates operational statistics for the workspace post pipeline.
/// </summary>
public sealed class GetPostsStatisticsQueryHandler : IRequestHandler<GetPostsStatisticsQuery, Result<PostStatisticsDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetPostsStatisticsQueryHandler> _logger;

    /// <summary>
    /// Initializes the post-statistics query handler.
    /// </summary>
    public GetPostsStatisticsQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        ILogger<GetPostsStatisticsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostStatisticsDto>> Handle(GetPostsStatisticsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostStatisticsDto>.Fail(access.Error!, access.Code!.Value);
            }

            var cacheStamp = await PostWorkflow.GetWorkspaceCacheStampAsync(_cacheService, _currentUserContext.WorkspaceId, cancellationToken);
            var cacheKey = PostWorkflow.BuildStatisticsCacheKey(_currentUserContext.WorkspaceId, request.From, request.To, cacheStamp);
            var cached = await _cacheService.GetAsync<PostStatisticsDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<PostStatisticsDto>.Ok(cached);
            }

            var posts = await _postRepository.GetByWorkspaceIdAsync(
                _currentUserContext.WorkspaceId,
                fromDate: request.From,
                toDate: request.To,
                ct: cancellationToken);

            var statistics = new PostStatisticsDto(
                posts.Count(post => post.Status == PostStatus.Draft),
                posts.Count(post => post.Status == PostStatus.Scheduled),
                posts.Count(post => post.Status == PostStatus.Published),
                posts.Count(post => post.Status is PostStatus.Failed or PostStatus.PartiallyFailed),
                posts.Sum(post => post.Targets.Count));

            await _cacheService.SetAsync(cacheKey, statistics, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<PostStatisticsDto>.Ok(statistics);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading post statistics for workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<PostStatisticsDto>.Fail("An unexpected post-statistics lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns one target publication status projection after verifying tenant ownership.
/// </summary>
public sealed class GetPostTargetStatusQueryHandler : IRequestHandler<GetPostTargetStatusQuery, Result<PostTargetDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetPostTargetStatusQueryHandler> _logger;

    /// <summary>
    /// Initializes the target-status query handler.
    /// </summary>
    public GetPostTargetStatusQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper,
        ILogger<GetPostTargetStatusQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostTargetDto>> Handle(GetPostTargetStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var target = await _postTargetRepository.GetByIdAsync(request.PostTargetId, cancellationToken);
            if (target is null)
            {
                return ContentGuard.NotFound<PostTargetDto>("Post target");
            }

            var post = await _postRepository.GetByIdAsync(target.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PostTargetDto>("Post");
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostTargetDto>.Fail(access.Error!, access.Code!.Value);
            }

            return Result<PostTargetDto>.Ok(_mapper.Map<PostTargetDto>(target));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading status for post target {PostTargetId}.", request.PostTargetId);
            return Result<PostTargetDto>.Fail("An unexpected post-target lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns paginated publishing-job history for a single post.
/// </summary>
public sealed class GetPublishingHistoryQueryHandler : IRequestHandler<GetPublishingHistoryQuery, Result<PagedResult<PublishingJobDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IPublishingJobRepository _publishingJobRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ILogger<GetPublishingHistoryQueryHandler> _logger;

    /// <summary>
    /// Initializes the publishing-history query handler.
    /// </summary>
    public GetPublishingHistoryQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IPublishingJobRepository publishingJobRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ILogger<GetPublishingHistoryQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _publishingJobRepository = publishingJobRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<PublishingJobDto>>> Handle(GetPublishingHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PagedResult<PublishingJobDto>>("Post");
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PagedResult<PublishingJobDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var jobs = await _publishingJobRepository.GetByPostIdAsync(post.Id, cancellationToken);
            var page = Math.Max(1, request.Pagination.Page);
            var pageSize = Math.Max(1, request.Pagination.PageSize);
            var items = jobs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(PostWorkflow.MapPublishingJob)
                .ToArray();

            return Result<PagedResult<PublishingJobDto>>.Ok(new PagedResult<PublishingJobDto>(items, jobs.Count, page, pageSize));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading publishing history for post {PostId}.", request.PostId);
            return Result<PagedResult<PublishingJobDto>>.Fail("An unexpected publishing-history lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns paginated failed publication attempts for operational monitoring in the current workspace.
/// </summary>
public sealed class GetFailedPublicationsQueryHandler : IRequestHandler<GetFailedPublicationsQuery, Result<PagedResult<PublishingJobDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPublishingJobRepository _publishingJobRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ILogger<GetFailedPublicationsQueryHandler> _logger;

    /// <summary>
    /// Initializes the failed-publications query handler.
    /// </summary>
    public GetFailedPublicationsQueryHandler(
        ICurrentUserContext currentUserContext,
        IPublishingJobRepository publishingJobRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ILogger<GetFailedPublicationsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _publishingJobRepository = publishingJobRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<PublishingJobDto>>> Handle(GetFailedPublicationsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PagedResult<PublishingJobDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var jobs = await _publishingJobRepository.GetFailedByWorkspaceIdAsync(_currentUserContext.WorkspaceId, cancellationToken);
            var page = Math.Max(1, request.Pagination.Page);
            var pageSize = Math.Max(1, request.Pagination.PageSize);
            var items = jobs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(PostWorkflow.MapPublishingJob)
                .ToArray();

            return Result<PagedResult<PublishingJobDto>>.Ok(new PagedResult<PublishingJobDto>(items, jobs.Count, page, pageSize));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading failed publications for workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<PagedResult<PublishingJobDto>>.Fail("An unexpected failed-publications lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Centralizes post-pipeline helper logic such as cache stamping, aggregate status derivation and DTO projections.
/// </summary>
internal static class PostWorkflow
{
    private const string WorkspaceStampKeyPrefix = "posts:stamp:";
    private const string AnalyticsStampKeyPrefix = "analytics:stamp:";

    internal static string ComposeCaption(PostContent content)
    {
        var title = string.IsNullOrWhiteSpace(content.Title) ? null : content.Title.Trim();
        var description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description.Trim();
        var tags = content.Tags.Count == 0 ? null : string.Join(' ', content.Tags);

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { title, description, tags }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    internal static PostStatus? TryParsePostStatus(string? value)
        => Enum.TryParse<PostStatus>(value, true, out var parsed) ? parsed : null;

    internal static Platform? TryParsePlatform(string? value)
        => Enum.TryParse<Platform>(value, true, out var parsed) ? parsed : null;

    internal static PostStatus CalculateAggregateStatus(IEnumerable<PostTarget> targets)
    {
        var materialized = targets.ToArray();
        if (materialized.Length == 0)
        {
            return PostStatus.Failed;
        }

        if (materialized.All(target => target.Status == TargetStatus.Published))
        {
            return PostStatus.Published;
        }

        if (materialized.Any(target => target.Status is TargetStatus.Pending or TargetStatus.Publishing or TargetStatus.Retrying))
        {
            return PostStatus.Publishing;
        }

        if (materialized.Any(target => target.Status == TargetStatus.Published) &&
            materialized.Any(target => target.Status == TargetStatus.Failed))
        {
            return PostStatus.PartiallyFailed;
        }

        return materialized.All(target => target.Status == TargetStatus.Failed)
            ? PostStatus.Failed
            : PostStatus.Publishing;
    }

    internal static DateTime? CalculateCompletedAt(IEnumerable<PostTarget> targets, DateTime completedAtUtc)
    {
        var aggregateStatus = CalculateAggregateStatus(targets);
        return aggregateStatus is PostStatus.Published or PostStatus.PartiallyFailed or PostStatus.Failed
            ? completedAtUtc
            : null;
    }

    internal static PublishingJobDto MapPublishingJob(PublishingJob job)
    {
        long? durationMs = null;
        if (job.CompletedAt.HasValue)
        {
            durationMs = Convert.ToInt64((job.CompletedAt.Value - job.StartedAt).TotalMilliseconds);
        }

        return new PublishingJobDto(
            job.Id,
            job.PostTargetId,
            job.AttemptNumber,
            job.StartedAt,
            job.CompletedAt,
            durationMs,
            job.Outcome.ToString(),
            job.ErrorDetails,
            job.RawApiResponse,
            job.SchedulerJobId);
    }

    internal static async Task<string> GetWorkspaceCacheStampAsync(ICacheService cacheService, Guid workspaceId, CancellationToken ct)
    {
        var key = $"{WorkspaceStampKeyPrefix}{workspaceId}";
        var cached = await cacheService.GetAsync<string>(key, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        const string defaultStamp = "v1";
        await cacheService.SetAsync(key, defaultStamp, TimeSpan.FromDays(30), ct);
        return defaultStamp;
    }

    internal static async Task BumpWorkspaceCacheStampAsync(ICacheService cacheService, Guid workspaceId, CancellationToken ct)
    {
        var nextStamp = Guid.NewGuid().ToString("N");
        await cacheService.SetAsync($"{WorkspaceStampKeyPrefix}{workspaceId}", nextStamp, TimeSpan.FromDays(30), ct);
        await cacheService.SetAsync($"{AnalyticsStampKeyPrefix}{workspaceId}", nextStamp, TimeSpan.FromDays(30), ct);
    }

    internal static string BuildPostsCacheKey(Guid workspaceId, GetPostsPagedQuery request, string stamp)
        => $"posts:{workspaceId}:{stamp}:{request.Filter.Status}:{request.Filter.Platform}:{request.Filter.From:O}:{request.Filter.To:O}:{request.Filter.Search}:{request.Pagination.Page}:{request.Pagination.PageSize}";

    internal static string BuildPostDetailCacheKey(Guid postId, string stamp)
        => $"post:{postId}:{stamp}";

    internal static string BuildCalendarCacheKey(Guid workspaceId, DateTime from, DateTime to, string stamp)
        => $"posts-calendar:{workspaceId}:{stamp}:{from:O}:{to:O}";

    internal static string BuildStatisticsCacheKey(Guid workspaceId, DateTime? from, DateTime? to, string stamp)
        => $"posts-statistics:{workspaceId}:{stamp}:{from:O}:{to:O}";
}
