using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAutomationExecutionLogRepository"/>.
///
/// <para><b>How it works:</b>
/// The idempotency check uses the unique composite index (AutomationRuleId, ExternalTriggerEventId)
/// via <c>AnyAsync</c>. Log listing supports pagination with Skip/Take for the rule detail page.</para>
///
/// <para><b>Purpose:</b>
/// Provides the automation audit trail and the idempotency guard that prevents
/// re-processing the same webhook event for the same rule.</para>
/// </summary>
public class AutomationExecutionLogRepository(ApplicationDbContext db) : IAutomationExecutionLogRepository
{
    /// <summary>
    /// Adds the log entry to the change tracker. Actual INSERT on SaveChangesAsync.
    /// Called by the automation engine after processing every event, regardless of outcome.
    /// </summary>
    public async Task<AutomationExecutionLog> AddAsync(AutomationExecutionLog log, CancellationToken ct = default)
    {
        await db.AutomationExecutionLogs.AddAsync(log, ct);
        return log;
    }

    /// <summary>
    /// Lists execution logs for a rule with pagination, ordered newest first.
    /// AsNoTracking — the execution history page is read-only.
    /// </summary>
    public async Task<IReadOnlyList<AutomationExecutionLog>> GetByRuleIdAsync(
        Guid ruleId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await db.AutomationExecutionLogs.AsNoTracking()
            .Where(el => el.AutomationRuleId == ruleId)
            .OrderByDescending(el => el.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<AutomationExecutionLog>> GetFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default)
        => BuildFilteredRuleQuery(ruleId, outcome, from, to)
            .OrderByDescending(log => log.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ContinueWith(task => (IReadOnlyList<AutomationExecutionLog>)task.Result, ct);

    /// <inheritdoc />
    public Task<int> CountFilteredByRuleIdAsync(
        Guid ruleId,
        AutomationExecutionOutcome? outcome,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
        => BuildFilteredRuleQuery(ruleId, outcome, from, to).CountAsync(ct);

    /// <summary>
    /// Uses AnyAsync (SELECT EXISTS) against the unique composite index.
    /// This is the idempotency guard — called before processing a webhook event
    /// to skip already-handled events. Fast due to the unique index.
    /// </summary>
    public async Task<bool> ExistsByExternalEventIdAsync(Guid ruleId, string externalTriggerEventId, CancellationToken ct = default)
        => await db.AutomationExecutionLogs.AsNoTracking()
            .AnyAsync(el =>
                el.AutomationRuleId == ruleId &&
                el.ExternalTriggerEventId == externalTriggerEventId, ct);

    /// <inheritdoc />
    public Task<int> CountActionedByRuleAndExternalUserAsync(Guid ruleId, string externalUserId, CancellationToken ct = default)
        => db.AutomationExecutionLogs.AsNoTracking()
            .CountAsync(
                log => log.AutomationRuleId == ruleId &&
                       log.TriggerExternalUserId == externalUserId &&
                       log.Outcome != AutomationExecutionOutcome.Failed,
                ct);

    /// <inheritdoc />
    public Task<int> CountByRuleAndOutcomeAsync(Guid ruleId, AutomationExecutionOutcome outcome, CancellationToken ct = default)
        => db.AutomationExecutionLogs.AsNoTracking()
            .CountAsync(log => log.AutomationRuleId == ruleId && log.Outcome == outcome, ct);

    /// <summary>
    /// Counts automation executions for all rules of a workspace within a UTC time window.
    /// </summary>
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
