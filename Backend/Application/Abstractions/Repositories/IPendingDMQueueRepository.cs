using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="PendingDMQueue"/> entity targeting the PendingDMQueueEntries table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Manages the queue of outbound direct messages that cannot be sent immediately (primarily due to target profile privacy settings on Instagram). The Stage 4 TRD requires message delay handling and background polling to dispatch pending DMs once conditions are met.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Relates <see cref="PendingDMQueue"/> to <see cref="SocialAccount"/> and <see cref="AutomationRule"/>.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Critical for maintaining trigger context and message queue state. Without it, messages intended for private accounts would simply be dropped, breaking user expectations.</para>
/// </remarks>
public interface IPendingDMQueueRepository
{
    /// <summary>
    /// Loads one tracked queue entry by identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enables cancellation or status updates on specific queued items.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an EF Core lookup including child SocialAccount and AutomationRule entities.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required for write-side workflows affecting individual queue entries.</para>
    /// </remarks>
    Task<PendingDMQueue?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all Waiting entries that haven't expired yet.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// The core query for the Hangfire polling job that scans and dispatches waiting DMs every 30 minutes.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Fetches entities where Status is Waiting and ExpiresAt has not passed, sorted in FIFO order (TriggeredAt).</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Tracked retrieval is vital since the Hangfire job needs to modify trigger counts and status changes directly in the unit of work.</para>
    /// </remarks>
    Task<IReadOnlyList<PendingDMQueue>> GetWaitingAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves one filtered page of queue entries for the current workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Populates paged list screens in the workspace dashboard showing queue status.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes AsNoTracking query filtered by WorkspaceId and optionally Status, applying Skip/Take paging.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures optimal memory consumption when listing large queues.</para>
    /// </remarks>
    Task<IReadOnlyList<PendingDMQueue>> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        PendingDMStatus? status,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts queue entries for the current workspace using an optional status filter.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Determines correct page counts for the workspace queue monitoring screen.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs a COUNT in SQL with the workspace and status criteria.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Efficiently queries postgres database without loading records.</para>
    /// </remarks>
    Task<int> CountByWorkspaceIdAsync(Guid workspaceId, PendingDMStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Persists a new pending DM entry.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Executed by the automation engine when a trigger matches but DM fails due to target profile settings.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds a new <see cref="PendingDMQueue"/> record to the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Core write operation for deferred delivery flows.</para>
    /// </remarks>
    Task<PendingDMQueue> AddAsync(PendingDMQueue entry, CancellationToken ct = default);

    /// <summary>
    /// Marks a queue entry as modified.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required when updating attempt counts, last checked times, or transitioning status.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Updates the entity state in the EF Core tracker to Modified.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Triggers updates upon Hangfire job polling runs.</para>
    /// </remarks>
    void Update(PendingDMQueue entry);

    /// <summary>
    /// Gets all entries whose ExpiresAt has passed while still in Waiting status.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Cleans up stale queued items, preventing infinite polling loops.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs a query filtered by Waiting status and ExpiresAt &lt;= utcNow.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Automated cleanup job uses this to transition items to Expired status.</para>
    /// </remarks>
    Task<IReadOnlyList<PendingDMQueue>> GetExpiredAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Gets all Waiting entries for a specific rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Used when rules are disabled or deleted to clean up pending queues.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Fetches all queue items in Waiting state matching the target AutomationRuleId.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents orphaned pending DMs from executing after their parent rule has been removed.</para>
    /// </remarks>
    Task<IReadOnlyList<PendingDMQueue>> GetWaitingByRuleIdAsync(Guid ruleId, CancellationToken ct = default);
}
