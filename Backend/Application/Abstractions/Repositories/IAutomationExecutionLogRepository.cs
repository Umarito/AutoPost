using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="AutomationExecutionLog"/> entity targeting the AutomationExecutionLogs table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Essential for compliance auditing, troubleshooting, and webhook message idempotency verification. The TRD specifies that we must log all execution attempts and prevent processing of duplicate external message/comment webhook triggers.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Logs are added sequentially. They refer back to <see cref="AutomationRule"/> and track key details about trigger events and platform actions.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Safeguards against duplicate platform operations and rate limiting by maintaining trigger execution state. Critical for SLA monitoring and troubleshooting.</para>
/// </remarks>
public interface IAutomationExecutionLogRepository
{
    /// <summary>
    /// Persists a new execution log entry in the AutomationExecutionLogs table.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Logs all execution outcomes (Success, Failed, Pending) for historical audit trails.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Inserts a new record into the change tracker in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required to keep an accurate execution history for rules.</para>
    /// </remarks>
    Task<AutomationExecutionLog> AddAsync(AutomationExecutionLog log, CancellationToken ct = default);

    /// <summary>
    /// Lists execution logs for a rule with pagination.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Powers the rule detail history view on the frontend.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Retrieves records ordered by ExecutedAt descending, applying skip/take offsets.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Critical for UI page loading without fetching the entire history, conserving server memory.</para>
    /// </remarks>
    Task<IReadOnlyList<AutomationExecutionLog>> GetByRuleIdAsync(Guid ruleId, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated list of filtered logs for a rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows the user to filter logs by outcome, date ranges, and perform granular debugging.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Filters the database rows dynamically. Employs AsNoTracking and orders results by date descending.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Enables debugging specific issues (such as API failures) in production.</para>
    /// </remarks>
    Task<IReadOnlyList<AutomationExecutionLog>> GetFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts execution logs matching the search criteria.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required for correct page count calculations on the filtered log grid.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Runs a count query matching the filters applied in <see cref="GetFilteredByRuleIdAsync"/>.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Critical for rendering pagination controls correctly.</para>
    /// </remarks>
    Task<int> CountFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies if a rule was already triggered by an external platform event to prevent duplicate execution.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Serves as the primary idempotency guard against duplicate webhook requests.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Runs an AnyAsync query checking for match on both AutomationRuleId and ExternalTriggerEventId.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Extremely critical to prevent duplicate auto-replies or actions from being executed multiple times, which would result in account suspensions on social networks.</para>
    /// </remarks>
    Task<bool> ExistsByExternalEventIdAsync(Guid ruleId, string externalTriggerEventId, CancellationToken ct = default);

    /// <summary>
    /// Counts successful or pending runs for a user to enforce rate limits and spam controls.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enforces auto-spam limits (e.g., maximum executions per external user within 24 hours).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries the logs table using AsNoTracking, counting entries matching RuleId, ExternalUserId, and non-Failed outcomes.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Protects connected social profiles from getting banned by third-party platforms for sending spam replies.</para>
    /// </remarks>
    Task<int> CountActionedByRuleAndExternalUserAsync(Guid ruleId, string externalUserId, CancellationToken ct = default);

    /// <summary>
    /// Counts logs matching a specific outcome for a rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Computes rule effectiveness metrics (e.g. success rate, failure rate).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries count filtered by RuleId and Outcome using AsNoTracking.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Drives analytics dashboard widgets regarding rule status.</para>
    /// </remarks>
    Task<int> CountByRuleAndOutcomeAsync(Guid ruleId, AutomationExecutionOutcome outcome, CancellationToken ct = default);

    /// <summary>
    /// Counts all execution logs inside a workspace within a UTC window.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Provides global workspace-level metrics and validates plan limits (e.g., total execution volume limits).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a count over matching logs within the date window, traversing the AutomationRule navigation to match WorkspaceId.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Indispensable for workspace invoicing and usage threshold billing policies.</para>
    /// </remarks>
    Task<int> CountByWorkspaceAndWindowAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);
}
