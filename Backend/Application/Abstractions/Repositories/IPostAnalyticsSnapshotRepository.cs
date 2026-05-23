using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="PostAnalyticsSnapshot"/> entity.
///
/// <para><b>Role in the system:</b>
/// PostAnalyticsSnapshot stores periodic metrics (views, likes, comments, shares, engagement rate)
/// for published posts. A Hangfire job collects snapshots at intervals: 1h, 24h, 7d, 30d after
/// publication. These time-series records power the post performance charts on the analytics page.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 6 — Analytics. "PostAnalyticsSnapshot — collected at intervals after publication."</para>
/// </summary>
public interface IPostAnalyticsSnapshotRepository
{
    /// <summary>
    /// Persists a new metrics snapshot. Called by the analytics-collection Hangfire job
    /// after fetching engagement metrics from the platform API.
    /// </summary>
    Task<PostAnalyticsSnapshot> AddAsync(PostAnalyticsSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all snapshots for a specific PostTarget, ordered chronologically.
    /// Used to render the post-performance chart showing how engagement metrics
    /// evolved over time (e.g., likes growing from 10 at 1h to 500 at 7d).
    /// Results are not tracked (read-only).
    /// </summary>
    Task<IReadOnlyList<PostAnalyticsSnapshot>> GetByPostTargetIdAsync(Guid postTargetId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the latest available analytics snapshot for every supplied post target.
    /// </summary>
    /// <param name="postTargetIds">Published post-target identifiers that should be aggregated.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The most recent snapshot per target.</returns>
    Task<IReadOnlyList<PostAnalyticsSnapshot>> GetLatestByPostTargetIdsAsync(
        IReadOnlyCollection<Guid> postTargetIds,
        CancellationToken ct = default);
}
