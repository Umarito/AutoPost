using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAutomationExecutionLogRepository"/> targeting the AutomationExecutionLogs table.
/// </summary>
/// <remarks>
/// <para><b>How it works:</b>
/// Interfaces with EF Core through <see cref="ApplicationDbContext"/>, utilizing tracking for inserts and AsNoTracking for read operations.</para>
/// <para><b>Purpose:</b>
/// Implements data-access operations for tracking rule triggers, ensuring idempotency and logging execution details.</para>
/// </remarks>
public class AutomationExecutionLogRepository(ApplicationDbContext db) : IAutomationExecutionLogRepository
{
    /// <summary>
    /// Adds a new execution log entry to the change tracker.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Logs all execution outcomes for compliance and troubleshooting logs.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Inserts log data into change tracking. Relies on DbContext save lifecycle.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Essential to preserve triggering event records.</para>
    /// </remarks>
    public async Task<AutomationExecutionLog> AddAsync(AutomationExecutionLog log, CancellationToken ct = default)
    {
        await db.AutomationExecutionLogs.AddAsync(log, ct);
        return log;
    }

    /// <summary>
    /// Lists execution logs for a rule with pagination.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Used by frontend grids displaying history.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking, orders descending by ExecutedAt, applying paging Skip/Take.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents high memory utilization when dealing with active rules with thousands of triggers.</para>
    /// </remarks>
    public async Task<IReadOnlyList<AutomationExecutionLog>> GetByRuleIdAsync(
        Guid ruleId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await db.AutomationExecutionLogs.AsNoTracking()
            .Where(el => el.AutomationRuleId == ruleId)
            .OrderByDescending(el => el.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>
    /// Retrieves a paginated list of filtered logs for a rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows users to narrow down log history based on dates and execution outcomes.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Constructs query using dynamic filters, ordering by ExecutedAt descending, and executes asynchronously.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Solves the continuation complexity and provides clean exception handling compared to task continuation.</para>
    /// </remarks>
    public async Task<IReadOnlyList<AutomationExecutionLog>> GetFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        return await BuildFilteredRuleQuery(ruleId, outcome, from, to)
            .OrderByDescending(log => log.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Counts execution logs matching the search criteria.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Essential to determine page counts in frontend UI tables.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Translates filters into a SQL COUNT query.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Avoids loading full entity records just to count rows.</para>
    /// </remarks>
    public Task<int> CountFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
        => BuildFilteredRuleQuery(ruleId, outcome, from, to).CountAsync(ct);

    /// <summary>
    /// Verifies if a rule was already triggered by an external platform event to prevent duplicate execution.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Essential to enforce idempotency at database level.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a direct exists query in SQL via EF Core AnyAsync.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Saves performance and avoids duplicate outbound messages.</para>
    /// </remarks>
    public async Task<bool> ExistsByExternalEventIdAsync(Guid ruleId, string externalTriggerEventId, CancellationToken ct = default)
        => await db.AutomationExecutionLogs.AsNoTracking()
            .AnyAsync(el =>
                el.AutomationRuleId == ruleId &&
                el.ExternalTriggerEventId == externalTriggerEventId, ct);

    /// <summary>
    /// Counts successful or pending runs for a user to enforce rate limits and spam controls.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Prevents flooding external users with automated DMs.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Filters logs by RuleId, TriggerExternalUserId and non-Failed status, executing COUNT in SQL.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Crucial to keep third-party APIs from blocking connected social handles.</para>
    /// </remarks>
    public Task<int> CountActionedByRuleAndExternalUserAsync(Guid ruleId, string externalUserId, CancellationToken ct = default)
        => db.AutomationExecutionLogs.AsNoTracking()
            .CountAsync(
                log => log.AutomationRuleId == ruleId &&
                       log.TriggerExternalUserId == externalUserId &&
                       log.Outcome != AutomationExecutionOutcome.Failed,
                ct);

    /// <summary>
    /// Counts logs matching a specific outcome for a rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives metrics regarding automation trigger success rates.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Filters by RuleId and Outcome, then counts via AsNoTracking query.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Avoids computing dashboard metrics in-memory.</para>
    /// </remarks>
    public Task<int> CountByRuleAndOutcomeAsync(Guid ruleId, AutomationExecutionOutcome outcome, CancellationToken ct = default)
        => db.AutomationExecutionLogs.AsNoTracking()
            .CountAsync(log => log.AutomationRuleId == ruleId && log.Outcome == outcome, ct);

    /// <summary>
    /// Counts all execution logs inside a workspace within a UTC window.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives overall workspace statistics and triggers threshold warnings.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Connects to AutomationRule table in SQL to verify WorkspaceId, then counts within window.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Crucial for billing validation and usage limiting.</para>
    /// </remarks>
    public async Task<int> CountByWorkspaceAndWindowAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default)
        => await db.AutomationExecutionLogs.AsNoTracking()
            .CountAsync(
                log => log.AutomationRule.WorkspaceId == workspaceId &&
                       log.ExecutedAt >= fromInclusiveUtc &&
                       log.ExecutedAt < toExclusiveUtc,
                ct);

    private IQueryable<AutomationExecutionLog> BuildFilteredRuleQuery(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to)
    {
        var query = db.AutomationExecutionLogs.AsNoTracking()
            .Where(log => log.AutomationRuleId == ruleId);

        if (outcome.HasValue)
        {
            query = query.Where(log => log.Outcome == outcome.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.ExecutedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.ExecutedAt <= to.Value);
        }

        return query;
    }
}
