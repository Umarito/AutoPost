using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPendingDMQueueRepository"/>.
///
/// <para><b>How it works:</b>
/// The Hangfire polling job calls <c>GetWaitingAsync</c> every 30 minutes to find DMs
/// that are still waiting to be sent. Each entry includes the SocialAccount (for API credentials)
/// and AutomationRule (for context). The job checks each external user's follow status and
/// sends the DM if they've followed back.</para>
///
/// <para><b>Purpose:</b>
/// Manages the deferred DM queue for private accounts — messages wait here until
/// the target user follows back, enabling DM delivery.</para>
/// </summary>
public class PendingDMQueueRepository(ApplicationDbContext db) : IPendingDMQueueRepository
{
    /// <inheritdoc />
    public async Task<PendingDMQueue?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PendingDMQueueEntries
            .Include(entry => entry.SocialAccount)
            .Include(entry => entry.AutomationRule)
            .FirstOrDefaultAsync(entry => entry.Id == id, ct);

    /// <summary>
    /// Finds all Waiting entries that haven't expired, with SocialAccount and AutomationRule loaded.
    /// Tracked — the job updates CheckAttemptCount, LastCheckedAt, and potentially Status.
    /// Ordered by trigger time (FIFO) to process oldest entries first.
    /// Hits the composite index (Status, ExpiresAt) for efficient filtering.
    /// </summary>
    public async Task<IReadOnlyList<PendingDMQueue>> GetWaitingAsync(CancellationToken ct = default)
        => await db.PendingDMQueueEntries
            .Include(q => q.SocialAccount)
            .Include(q => q.AutomationRule)
            .Where(q => q.Status == PendingDMStatus.Waiting && q.ExpiresAt > DateTime.UtcNow)
            .OrderBy(q => q.TriggeredAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingDMQueue>> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        PendingDMStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildWorkspaceQuery(workspaceId, status);

        return await query
            .OrderByDescending(entry => entry.TriggeredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> CountByWorkspaceIdAsync(Guid workspaceId, PendingDMStatus? status, CancellationToken ct = default)
        => BuildWorkspaceQuery(workspaceId, status).CountAsync(ct);

    /// <summary>
    /// Adds a new queue entry to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<PendingDMQueue> AddAsync(PendingDMQueue entry, CancellationToken ct = default)
    {
        await db.PendingDMQueueEntries.AddAsync(entry, ct);
        return entry;
    }

    /// <summary>
    /// Marks the entry as Modified for status transitions and check-attempt tracking.
    /// </summary>
    public void Update(PendingDMQueue entry)
        => db.PendingDMQueueEntries.Update(entry);

    /// <summary>
    /// Finds all entries whose expiration time has passed while still in Waiting status.
    /// The expiration job transitions these to Expired, preventing indefinite retries.
    /// </summary>
    public async Task<IReadOnlyList<PendingDMQueue>> GetExpiredAsync(DateTime utcNow, CancellationToken ct = default)
        => await db.PendingDMQueueEntries
            .Where(q => q.Status == PendingDMStatus.Waiting && q.ExpiresAt <= utcNow)
            .ToListAsync(ct);

    private IQueryable<PendingDMQueue> BuildWorkspaceQuery(Guid workspaceId, PendingDMStatus? status)
    {
        var query = db.PendingDMQueueEntries.AsNoTracking()
            .Include(entry => entry.SocialAccount)
            .Include(entry => entry.AutomationRule)
            .Where(entry => entry.AutomationRule.WorkspaceId == workspaceId);

        if (status.HasValue)
        {
            query = query.Where(entry => entry.Status == status.Value);
        }

        return query;
    }
}
