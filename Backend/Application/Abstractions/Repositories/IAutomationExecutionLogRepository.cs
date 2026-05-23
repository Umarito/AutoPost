using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="AutomationExecutionLog"/> entity.
///
/// <para><b>Role in the system:</b>
/// AutomationExecutionLog records every time an automation rule is triggered — whether it executed
/// successfully, was skipped (duplicate, daily limit reached), or failed (API error). The logs serve
/// two purposes: (1) idempotency — preventing re-processing of the same platform event, and
/// (2) debugging — showing the rule owner exactly what happened and why.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Automation. Endpoint: GET /api/automation/rules/{id}/logs.
/// "ExternalTriggerEventId used as idempotency key."</para>
/// </summary>
public interface IAutomationExecutionLogRepository
{
    /// <summary>
    /// Persists a new execution log entry. Called by the automation engine after
    /// evaluating a rule against an incoming event, regardless of outcome.
    /// </summary>
    Task<AutomationExecutionLog> AddAsync(AutomationExecutionLog log, CancellationToken ct = default);

    /// <summary>
    /// Lists execution logs for a specific rule with pagination, ordered by time (newest first).
    /// Used to render the "Execution History" tab on the rule detail page.
    /// Results are not tracked (read-only).
    /// </summary>
    /// <param name="ruleId">The automation rule to fetch logs for.</param>
    /// <param name="skip">Number of entries to skip (for pagination).</param>
    /// <param name="take">Number of entries to return (page size, default 50).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task<IReadOnlyList<AutomationExecutionLog>> GetByRuleIdAsync(Guid ruleId, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Lists filtered execution logs for one rule with paging.
    /// </summary>
    Task<IReadOnlyList<AutomationExecutionLog>> GetFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts filtered execution logs for one rule.
    /// </summary>
    Task<int> CountFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a log entry already exists for the given rule + external event combination.
    /// This is the idempotency guard — if the webhook delivers the same comment/follow event twice,
    /// the system uses this check to skip re-execution and return an "Already processed" response.
    /// </summary>
    /// <param name="ruleId">The automation rule to check against.</param>
    /// <param name="externalTriggerEventId">The platform-side event ID (e.g., comment ID).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> if this event was already processed; <c>false</c> otherwise.</returns>
    Task<bool> ExistsByExternalEventIdAsync(Guid ruleId, string externalTriggerEventId, CancellationToken ct = default);

    /// <summary>
    /// Counts successful or queued executions for one external user to enforce anti-spam limits.
    /// </summary>
    Task<int> CountActionedByRuleAndExternalUserAsync(Guid ruleId, string externalUserId, CancellationToken ct = default);

    /// <summary>
    /// Counts executions of a specific outcome for one rule.
    /// </summary>
    Task<int> CountByRuleAndOutcomeAsync(Guid ruleId, AutomationExecutionOutcome outcome, CancellationToken ct = default);

    /// <summary>
    /// Counts execution-log entries for all automation rules inside a workspace during a selected UTC window.
    /// </summary>
    /// <param name="workspaceId">Workspace whose automation activity should be measured.</param>
    /// <param name="fromInclusiveUtc">Inclusive UTC lower boundary.</param>
    /// <param name="toExclusiveUtc">Exclusive UTC upper boundary.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of automation executions recorded in the window.</returns>
    Task<int> CountByWorkspaceAndWindowAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);
}
