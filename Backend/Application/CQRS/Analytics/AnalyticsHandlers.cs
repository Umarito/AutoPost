using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Analytics;
using Application.DTOs.Post;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Analytics;

/// <summary>
/// Collects a durable analytics snapshot for a published post target using provider APIs and protected credentials.
/// </summary>
public sealed class CollectPostSnapshotCommandHandler : IRequestHandler<CollectPostSnapshotCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostTargetRepository _postTargetRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPlatformIntegrationService _platformIntegrationService;
    private readonly IPostAnalyticsSnapshotRepository _postAnalyticsSnapshotRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CollectPostSnapshotCommandHandler> _logger;

    /// <summary>
    /// Initializes the snapshot-collection handler.
    /// </summary>
    public CollectPostSnapshotCommandHandler(
        ICurrentUserContext currentUserContext,
        IPostTargetRepository postTargetRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPlatformIntegrationService platformIntegrationService,
        IPostAnalyticsSnapshotRepository postAnalyticsSnapshotRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<CollectPostSnapshotCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postTargetRepository = postTargetRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _platformIntegrationService = platformIntegrationService;
        _postAnalyticsSnapshotRepository = postAnalyticsSnapshotRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(CollectPostSnapshotCommand request, CancellationToken cancellationToken)
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
                var access = await ContentGuard.RequireReadAccessAsync(
                    _currentUserContext.UserId,
                    post.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            if (target.Status != TargetStatus.Published || string.IsNullOrWhiteSpace(target.Result?.ExternalPostId))
            {
                return Result.Fail("Analytics can only be collected for published targets with a remote post identifier.", ErrorCode.Conflict);
            }

            var snapshot = await _platformIntegrationService.GetPostAnalyticsAsync(
                target.SocialAccount,
                target.Result.ExternalPostId,
                cancellationToken);

            await _postAnalyticsSnapshotRepository.AddAsync(
                PostAnalyticsSnapshot.Create(
                    target.Id,
                    snapshot.RecordedAtUtc,
                    snapshot.Views,
                    snapshot.Likes,
                    snapshot.Comments,
                    snapshot.Shares,
                    snapshot.Saves,
                    snapshot.Reach,
                    snapshot.Impressions,
                    snapshot.AverageWatchTime,
                    snapshot.CompletionRate),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AnalyticsWorkflow.BumpWorkspaceAnalyticsStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while collecting analytics snapshot for post target {PostTargetId}.", request.PostTargetId);
            return Result.Fail("An unexpected analytics-collection error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns cross-platform analytics for a single post by composing the latest snapshot and full timeline per target.
/// </summary>
public sealed class GetPostAnalyticsQueryHandler : IRequestHandler<GetPostAnalyticsQuery, Result<PostAnalyticsDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPostAnalyticsSnapshotRepository _postAnalyticsSnapshotRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetPostAnalyticsQueryHandler> _logger;

    /// <summary>
    /// Initializes the post-analytics query handler.
    /// </summary>
    public GetPostAnalyticsQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPostAnalyticsSnapshotRepository postAnalyticsSnapshotRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetPostAnalyticsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _postAnalyticsSnapshotRepository = postAnalyticsSnapshotRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PostAnalyticsDto>> Handle(GetPostAnalyticsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _postRepository.GetByIdWithTargetsAsync(request.PostId, cancellationToken);
            if (post is null)
            {
                return ContentGuard.NotFound<PostAnalyticsDto>("Post");
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                post.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PostAnalyticsDto>.Fail(access.Error!, access.Code!.Value);
            }

            var stamp = await AnalyticsWorkflow.GetWorkspaceAnalyticsStampAsync(_cacheService, post.WorkspaceId, cancellationToken);
            var cacheKey = AnalyticsWorkflow.BuildPostAnalyticsCacheKey(post.Id, stamp);
            var cached = await _cacheService.GetAsync<PostAnalyticsDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<PostAnalyticsDto>.Ok(cached);
            }

            var byPlatform = new List<PlatformAnalyticsDto>(post.Targets.Count);

            foreach (var target in post.Targets.OrderBy(item => item.Platform))
            {
                var timeline = await _postAnalyticsSnapshotRepository.GetByPostTargetIdAsync(target.Id, cancellationToken);
                var latest = timeline.LastOrDefault();

                byPlatform.Add(new PlatformAnalyticsDto(
                    target.Platform.ToString(),
                    latest?.Views ?? 0,
                    latest?.Likes ?? 0,
                    latest?.Comments ?? 0,
                    latest?.Shares ?? 0,
                    latest?.Reach,
                    latest?.AverageWatchTime,
                    latest?.CompletionRate,
                    timeline.Select(_mapper.Map<PostAnalyticsSnapshotDto>).ToList()));
            }

            var result = new PostAnalyticsDto(post.Id, post.Content.Title ?? "Untitled post", byPlatform);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<PostAnalyticsDto>.Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading analytics for post {PostId}.", request.PostId);
            return Result<PostAnalyticsDto>.Fail("An unexpected post-analytics lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Builds a cache-aside workspace dashboard summary without N+1 fan-out by using aggregate repository queries.
/// </summary>
public sealed class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPostRepository _postRepository;
    private readonly ISocialAccountInsightRepository _socialAccountInsightRepository;
    private readonly IPostAnalyticsSnapshotRepository _postAnalyticsSnapshotRepository;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IAutomationExecutionLogRepository _automationExecutionLogRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetDashboardSummaryQueryHandler> _logger;

    /// <summary>
    /// Initializes the dashboard-summary query handler.
    /// </summary>
    public GetDashboardSummaryQueryHandler(
        ICurrentUserContext currentUserContext,
        IPostRepository postRepository,
        ISocialAccountInsightRepository socialAccountInsightRepository,
        IPostAnalyticsSnapshotRepository postAnalyticsSnapshotRepository,
        IInboxConversationRepository inboxConversationRepository,
        IAutomationExecutionLogRepository automationExecutionLogRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetDashboardSummaryQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _postRepository = postRepository;
        _socialAccountInsightRepository = socialAccountInsightRepository;
        _postAnalyticsSnapshotRepository = postAnalyticsSnapshotRepository;
        _inboxConversationRepository = inboxConversationRepository;
        _automationExecutionLogRepository = automationExecutionLogRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
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
                return Result<DashboardSummaryDto>.Fail(access.Error!, access.Code!.Value);
            }

            var fromInclusiveUtc = request.From ?? AnalyticsWorkflow.GetStartOfWeekUtc(DateTime.UtcNow);
            var toInclusiveUtc = request.To ?? DateTime.UtcNow;
            var stamp = await AnalyticsWorkflow.GetWorkspaceAnalyticsStampAsync(_cacheService, _currentUserContext.WorkspaceId, cancellationToken);
            var cacheKey = AnalyticsWorkflow.BuildDashboardCacheKey(_currentUserContext.WorkspaceId, fromInclusiveUtc, toInclusiveUtc, stamp);
            var cached = await _cacheService.GetAsync<DashboardSummaryDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<DashboardSummaryDto>.Ok(cached);
            }

            var allPosts = await _postRepository.GetByWorkspaceIdAsync(_currentUserContext.WorkspaceId, ct: cancellationToken);
            var postsCompletedInWindow = allPosts
                .Where(post => post.CompletedAt.HasValue &&
                               post.CompletedAt.Value >= fromInclusiveUtc &&
                               post.CompletedAt.Value <= toInclusiveUtc)
                .ToArray();

            var postsScheduled = allPosts.Count(post => post.Status == PostStatus.Scheduled);
            var targetIds = postsCompletedInWindow
                .SelectMany(post => post.Targets.Select(target => target.Id))
                .Distinct()
                .ToArray();

            var latestTargetSnapshots = await _postAnalyticsSnapshotRepository.GetLatestByPostTargetIdsAsync(targetIds, cancellationToken);
            var totalReach = latestTargetSnapshots.Sum(snapshot => snapshot.Reach ?? snapshot.Impressions ?? 0);

            var groupedViewsByPostId = postsCompletedInWindow
                .Select(post => new
                {
                    Post = post,
                    Views = latestTargetSnapshots
                        .Where(snapshot => post.Targets.Any(target => target.Id == snapshot.PostTargetId))
                        .Sum(snapshot => snapshot.Views)
                })
                .OrderByDescending(item => item.Views)
                .ToArray();

            PostSummaryDto? topPost = groupedViewsByPostId.Length == 0
                ? null
                : _mapper.Map<PostSummaryDto>(groupedViewsByPostId[0].Post);

            var insightWindow = await _socialAccountInsightRepository.GetByWorkspaceIdInRangeAsync(
                _currentUserContext.WorkspaceId,
                fromInclusiveUtc,
                toInclusiveUtc,
                cancellationToken);

            var newFollowers = insightWindow
                .GroupBy(snapshot => snapshot.SocialAccountId)
                .Sum(group =>
                {
                    var ordered = group.OrderBy(item => item.RecordedAt).ToArray();
                    return ordered.Length < 2
                        ? 0
                        : Convert.ToInt32(ordered[^1].FollowersCount - ordered[0].FollowersCount);
                });

            var openConversations = (await _inboxConversationRepository.GetByWorkspaceIdAsync(
                _currentUserContext.WorkspaceId,
                status: ConversationStatus.Open,
                ct: cancellationToken)).Count;

            var startOfTodayUtc = DateTime.UtcNow.Date;
            var automationTriggeredToday = await _automationExecutionLogRepository.CountByWorkspaceAndWindowAsync(
                _currentUserContext.WorkspaceId,
                startOfTodayUtc,
                startOfTodayUtc.AddDays(1),
                cancellationToken);

            var summary = new DashboardSummaryDto(
                postsCompletedInWindow.Length,
                postsScheduled,
                totalReach,
                newFollowers,
                openConversations,
                automationTriggeredToday,
                topPost);

            await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<DashboardSummaryDto>.Ok(summary);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading dashboard analytics for workspace {WorkspaceId}.", _currentUserContext.WorkspaceId);
            return Result<DashboardSummaryDto>.Fail("An unexpected dashboard-analytics lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Centralizes analytics cache-stamp helpers so dashboard and post metrics invalidate without wildcard Redis deletes.
/// </summary>
internal static class AnalyticsWorkflow
{
    private const string AnalyticsStampKeyPrefix = "analytics:stamp:";

    internal static DateTime GetStartOfWeekUtc(DateTime utcNow)
    {
        var dayOffset = ((int)utcNow.DayOfWeek + 6) % 7;
        return utcNow.Date.AddDays(-dayOffset);
    }

    internal static async Task<string> GetWorkspaceAnalyticsStampAsync(ICacheService cacheService, Guid workspaceId, CancellationToken ct)
    {
        var key = $"{AnalyticsStampKeyPrefix}{workspaceId}";
        var cached = await cacheService.GetAsync<string>(key, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        const string defaultStamp = "v1";
        await cacheService.SetAsync(key, defaultStamp, TimeSpan.FromDays(30), ct);
        return defaultStamp;
    }

    internal static Task BumpWorkspaceAnalyticsStampAsync(ICacheService cacheService, Guid workspaceId, CancellationToken ct)
        => cacheService.SetAsync($"{AnalyticsStampKeyPrefix}{workspaceId}", Guid.NewGuid().ToString("N"), TimeSpan.FromDays(30), ct);

    internal static string BuildPostAnalyticsCacheKey(Guid postId, string stamp)
        => $"post-analytics:{postId}:{stamp}";

    internal static string BuildDashboardCacheKey(Guid workspaceId, DateTime fromInclusiveUtc, DateTime toInclusiveUtc, string stamp)
        => $"dashboard-summary:{workspaceId}:{stamp}:{fromInclusiveUtc:O}:{toInclusiveUtc:O}";
}
