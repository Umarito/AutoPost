using Application.DTOs.Post;

namespace Application.DTOs.Analytics;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  ANALYTICS DTOs — Dashboard, Post Metrics, Account Insights                ║
// ║  TRD Stage 6: Analytics & Growth                                           ║
// ║  Endpoints: GET /api/analytics/dashboard, .../posts/{id}, .../insights     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Aggregated dashboard summary shown on the workspace home page.
///
/// <para><b>What it contains:</b>
/// Key performance indicators for the current week: post count, reach, follower growth,
/// open inbox conversations, and automation activity. Also includes the top-performing
/// post of the week for quick access.</para>
///
/// <para><b>How it's built:</b>
/// The AnalyticsService aggregates data from multiple repositories (Posts, SocialAccountInsights,
/// InboxConversations, AutomationExecutionLogs) to compose this single summary DTO.</para>
///
/// <para><b>TRD API:</b> GET /api/analytics/dashboard</para>
/// </summary>
/// <param name="PostsThisWeek">Number of posts published in the current week.</param>
/// <param name="PostsScheduled">Number of posts currently in Scheduled status (upcoming).</param>
/// <param name="TotalReachThisWeek">Combined reach across all platforms this week.</param>
/// <param name="NewFollowersThisWeek">Net new followers gained across all accounts this week.</param>
/// <param name="OpenConversations">Number of inbox conversations currently in Open status.</param>
/// <param name="AutomationsTriggeredToday">Number of automation rule executions today.</param>
/// <param name="TopPostThisWeek">The post with the most views this week, or null if no posts.</param>
public record DashboardSummaryDto(
    int PostsThisWeek,
    int PostsScheduled,
    long TotalReachThisWeek,
    int NewFollowersThisWeek,
    int OpenConversations,
    int AutomationsTriggeredToday,
    PostSummaryDto? TopPostThisWeek
);

/// <summary>
/// Aggregated analytics for a single published post across all target platforms.
///
/// <para><b>What it contains:</b>
/// Post identification and a breakdown of engagement metrics per platform.
/// A post targeting YouTube + Instagram will have two <see cref="PlatformAnalyticsDto"/>
/// entries, each with independent metrics and timeline.</para>
///
/// <para><b>TRD API:</b> GET /api/analytics/posts/{id}</para>
/// </summary>
/// <param name="PostId">The post's unique identifier.</param>
/// <param name="Title">The post's title (for display in analytics header).</param>
/// <param name="ByPlatform">Per-platform metrics breakdown with timeline data.</param>
public record PostAnalyticsDto(
    Guid PostId,
    string Title,
    List<PlatformAnalyticsDto> ByPlatform
);

/// <summary>
/// Engagement metrics for a single platform target of a published post.
///
/// <para><b>What it contains:</b>
/// Cumulative engagement metrics (views, likes, comments, shares) plus platform-specific
/// metrics (reach, average watch time, completion rate) and a historical timeline
/// of how these metrics evolved over time.</para>
///
/// <para><b>How timeline works:</b>
/// The Hangfire analytics-collection job creates snapshots at intervals after publication:
/// 1 hour, 24 hours, 7 days, 30 days. The timeline shows the growth trajectory.</para>
/// </summary>
/// <param name="Platform">Platform name: "YouTube", "Instagram", etc.</param>
/// <param name="Views">Total view/impression count.</param>
/// <param name="Likes">Total likes/reactions count.</param>
/// <param name="Comments">Total comments/replies count.</param>
/// <param name="Shares">Total shares/reposts count.</param>
/// <param name="Reach">Unique accounts reached, or null if not available for this platform.</param>
/// <param name="AverageWatchTime">Average watch time in seconds (video platforms), or null.</param>
/// <param name="CompletionRate">Percentage of viewers who watched till the end (0.0-1.0), or null.</param>
/// <param name="Timeline">Chronological snapshots showing metric evolution over time.</param>
public record PlatformAnalyticsDto(
    string Platform,
    long Views,
    long Likes,
    long Comments,
    long Shares,
    long? Reach,
    double? AverageWatchTime,
    double? CompletionRate,
    List<PostAnalyticsSnapshotDto> Timeline
);

/// <summary>
/// A single point-in-time snapshot of post engagement metrics.
/// Used to build the post performance timeline chart.
/// </summary>
/// <param name="RecordedAt">UTC timestamp when this snapshot was captured.</param>
/// <param name="Views">View count at this point in time.</param>
/// <param name="Likes">Like count at this point in time.</param>
/// <param name="Comments">Comment count at this point in time.</param>
/// <param name="Shares">Share count at this point in time.</param>
public record PostAnalyticsSnapshotDto(
    DateTime RecordedAt,
    long Views,
    long Likes,
    long Comments,
    long Shares
);

/// <summary>
/// A single point-in-time snapshot of social account growth metrics.
/// Used to build the follower growth chart on the dashboard.
///
/// <para><b>TRD API:</b> GET /api/analytics/insights?accountId=...&amp;from=...&amp;to=...</para>
/// </summary>
/// <param name="RecordedAt">UTC timestamp when this snapshot was captured (typically once per day).</param>
/// <param name="FollowersCount">Total follower/subscriber count at this point in time.</param>
/// <param name="Reach">Account reach for this day, or null if not available.</param>
/// <param name="Impressions">Account impressions for this day, or null if not available.</param>
public record InsightDto(
    DateTime RecordedAt,
    long FollowersCount,
    long? Reach,
    long? Impressions
);
