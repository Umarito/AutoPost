using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for <see cref="SocialAccountInsight"/> time-series data.
/// Stores daily analytics snapshots (followers, reach) for growth charts on the dashboard.
/// TRD Stage 6 — Analytics &amp; Growth.
/// </summary>
public interface ISocialAccountInsightRepository
{
    /// <summary>
    /// Persists a new analytics snapshot. Called by the daily Hangfire collection job.
    /// </summary>
    Task<SocialAccountInsight> AddAsync(SocialAccountInsight insight, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the most recent snapshot for a social account.
    /// Used on the dashboard to show current follower count without a live API call.
    /// </summary>
    Task<SocialAccountInsight?> GetLatestByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all snapshots within a date range for rendering growth charts.
    /// Results are ordered chronologically (earliest first) and are not tracked.
    /// </summary>
    Task<IReadOnlyList<SocialAccountInsight>> GetByAccountIdInRangeAsync(Guid socialAccountId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the latest available insight snapshot for every social account in a workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace whose connected-account snapshots should be loaded.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The most recent snapshot per social account.</returns>
    Task<IReadOnlyList<SocialAccountInsight>> GetLatestByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all account-level insight snapshots captured for a workspace during a selected UTC window.
    /// </summary>
    /// <param name="workspaceId">Workspace whose insight history should be loaded.</param>
    /// <param name="fromInclusiveUtc">Inclusive UTC lower boundary.</param>
    /// <param name="toInclusiveUtc">Inclusive UTC upper boundary.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>Chronologically ordered snapshots across all connected social accounts of the workspace.</returns>
    Task<IReadOnlyList<SocialAccountInsight>> GetByWorkspaceIdInRangeAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toInclusiveUtc,
        CancellationToken ct = default);
}
