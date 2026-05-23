using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="PendingDMQueue"/> entity.
///
/// <para><b>Role in the system:</b>
/// PendingDMQueue is the waiting room for direct messages that cannot be sent immediately.
/// The most common reason is that the target user has a private account — Instagram doesn't
/// allow DMs to users who don't follow you. The system queues the message and a Hangfire job
/// checks every 30 minutes whether the user has followed back, then sends the DM.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Automation. "Hangfire job every 30 min scans Waiting entries;
/// ExpiresAt default = TriggeredAt + 7 days."</para>
/// </summary>
public interface IPendingDMQueueRepository
{
    /// <summary>
    /// Loads one tracked queue entry by identifier.
    /// </summary>
    Task<PendingDMQueue?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all Waiting entries that haven't expired yet.
    /// This is the core query for the Hangfire polling job — it finds all messages
    /// still waiting to be sent and checks each one's follow status via the platform API.
    /// Entities are tracked because the job will update CheckAttemptCount and LastCheckedAt.
    /// </summary>
    Task<IReadOnlyList<PendingDMQueue>> GetWaitingAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves one filtered page of queue entries for the current workspace.
    /// </summary>
    Task<IReadOnlyList<PendingDMQueue>> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        PendingDMStatus? status,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts queue entries for the current workspace using an optional status filter.
    /// </summary>
    Task<int> CountByWorkspaceIdAsync(Guid workspaceId, PendingDMStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Persists a new pending DM entry. Called when the automation engine determines
    /// that a DM cannot be sent immediately (e.g., target account is private).
    /// The message text is pre-resolved (variables substituted at trigger time).
    /// </summary>
    Task<PendingDMQueue> AddAsync(PendingDMQueue entry, CancellationToken ct = default);

    /// <summary>
    /// Marks a queue entry as modified. Typical updates: status transitions
    /// (Waiting → Sent/Expired/Cancelled), incrementing CheckAttemptCount,
    /// updating LastCheckedAt after each poll.
    /// </summary>
    void Update(PendingDMQueue entry);

    /// <summary>
    /// Gets all entries whose ExpiresAt has passed while still in Waiting status.
    /// Called by the expiration job to transition them to Expired status,
    /// preventing indefinite retries for messages that will never be delivered.
    /// </summary>
    Task<IReadOnlyList<PendingDMQueue>> GetExpiredAsync(DateTime utcNow, CancellationToken ct = default);
}
