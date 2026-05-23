using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISocialAccountInsightRepository"/>.
///
/// <para><b>How it works:</b>
/// Stores and retrieves daily analytics snapshots using the composite index
/// (SocialAccountId, RecordedAt) for efficient time-range queries.</para>
///
/// <para><b>Purpose:</b>
/// Feeds the dashboard growth charts with historical follower/reach/impression data.</para>
/// </summary>
public class SocialAccountInsightRepository(ApplicationDbContext db) : ISocialAccountInsightRepository
{
    /// <summary>
    /// Adds the snapshot to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<SocialAccountInsight> AddAsync(SocialAccountInsight insight, CancellationToken ct = default)
    {
        await db.SocialAccountInsights.AddAsync(insight, ct);
        return insight;
    }

    /// <summary>
    /// Retrieves the most recent snapshot by ordering descending on RecordedAt and taking the first.
    /// Uses the (SocialAccountId, RecordedAt) composite index for an efficient ORDER BY + LIMIT 1.
    /// AsNoTracking — read-only for dashboard display.
    /// </summary>
    public async Task<SocialAccountInsight?> GetLatestByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default)
        => await db.SocialAccountInsights.AsNoTracking()
            .Where(i => i.SocialAccountId == socialAccountId)
            .OrderByDescending(i => i.RecordedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Retrieves all snapshots in a date range using the composite index.
    /// Results are ordered chronologically (ascending) for chart rendering — the frontend
    /// plots data points from left (oldest) to right (newest).
    /// AsNoTracking — read-only for chart data.
    /// </summary>
    public async Task<IReadOnlyList<SocialAccountInsight>> GetByAccountIdInRangeAsync(
        Guid socialAccountId, DateTime from, DateTime to, CancellationToken ct = default)
        => await db.SocialAccountInsights.AsNoTracking()
            .Where(i => i.SocialAccountId == socialAccountId && i.RecordedAt >= from && i.RecordedAt <= to)
            .OrderBy(i => i.RecordedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Retrieves the freshest insight row for every social account that belongs to the workspace.
    /// </summary>
    public async Task<IReadOnlyList<SocialAccountInsight>> GetLatestByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var latestTimestamps = db.SocialAccountInsights
            .Where(insight => insight.SocialAccount.WorkspaceId == workspaceId)
            .GroupBy(insight => insight.SocialAccountId)
            .Select(group => new
            {
                SocialAccountId = group.Key,
                RecordedAt = group.Max(item => item.RecordedAt)
            });

        return await db.SocialAccountInsights
            .AsNoTracking()
            .Where(insight => insight.SocialAccount.WorkspaceId == workspaceId)
            .Join(
                latestTimestamps,
                insight => new { insight.SocialAccountId, insight.RecordedAt },
                latest => new { latest.SocialAccountId, latest.RecordedAt },
                (insight, _) => insight)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Retrieves all insight snapshots for the workspace within a UTC range.
    /// </summary>
    public async Task<IReadOnlyList<SocialAccountInsight>> GetByWorkspaceIdInRangeAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toInclusiveUtc,
        CancellationToken ct = default)
        => await db.SocialAccountInsights
            .AsNoTracking()
            .Where(insight =>
                insight.SocialAccount.WorkspaceId == workspaceId &&
                insight.RecordedAt >= fromInclusiveUtc &&
                insight.RecordedAt <= toInclusiveUtc)
            .OrderBy(insight => insight.RecordedAt)
            .ToListAsync(ct);
}
