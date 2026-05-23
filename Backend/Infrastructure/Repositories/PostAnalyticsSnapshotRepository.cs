using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPostAnalyticsSnapshotRepository"/>.
///
/// <para><b>How it works:</b>
/// Stores periodic metrics snapshots using the composite index (PostTargetId, RecordedAt)
/// for efficient chronological queries.</para>
///
/// <para><b>Purpose:</b>
/// Feeds the post performance charts with engagement metrics collected at scheduled intervals
/// after publication (1h, 24h, 7d, 30d).</para>
/// </summary>
public class PostAnalyticsSnapshotRepository(ApplicationDbContext db) : IPostAnalyticsSnapshotRepository
{
    /// <summary>
    /// Adds the snapshot to the change tracker. Called by the analytics collection Hangfire job.
    /// </summary>
    public async Task<PostAnalyticsSnapshot> AddAsync(PostAnalyticsSnapshot snapshot, CancellationToken ct = default)
    {
        await db.PostAnalyticsSnapshots.AddAsync(snapshot, ct);
        return snapshot;
    }

    /// <summary>
    /// Lists all snapshots for a post target, ordered chronologically (earliest first).
    /// AsNoTracking — chart data is read-only.
    /// Uses the composite index (PostTargetId, RecordedAt) for efficient retrieval.
    /// </summary>
    public async Task<IReadOnlyList<PostAnalyticsSnapshot>> GetByPostTargetIdAsync(Guid postTargetId, CancellationToken ct = default)
        => await db.PostAnalyticsSnapshots.AsNoTracking()
            .Where(s => s.PostTargetId == postTargetId)
            .OrderBy(s => s.RecordedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Retrieves the newest analytics snapshot for every supplied post target.
    /// </summary>
    public async Task<IReadOnlyList<PostAnalyticsSnapshot>> GetLatestByPostTargetIdsAsync(
        IReadOnlyCollection<Guid> postTargetIds,
        CancellationToken ct = default)
    {
        if (postTargetIds.Count == 0)
        {
            return [];
        }

        var latestTimestamps = db.PostAnalyticsSnapshots
            .Where(snapshot => postTargetIds.Contains(snapshot.PostTargetId))
            .GroupBy(snapshot => snapshot.PostTargetId)
            .Select(group => new
            {
                PostTargetId = group.Key,
                RecordedAt = group.Max(item => item.RecordedAt)
            });

        return await db.PostAnalyticsSnapshots
            .AsNoTracking()
            .Where(snapshot => postTargetIds.Contains(snapshot.PostTargetId))
            .Join(
                latestTimestamps,
                snapshot => new { snapshot.PostTargetId, snapshot.RecordedAt },
                latest => new { latest.PostTargetId, latest.RecordedAt },
                (snapshot, _) => snapshot)
            .ToListAsync(ct);
    }
}
