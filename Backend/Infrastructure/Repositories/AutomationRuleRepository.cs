using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRuleRepository"/>.
///
/// <para><b>How it works:</b>
/// The critical path is webhook processing — <c>GetEnabledByAccountIdAsync</c> loads all enabled
/// rules for an account with their Conditions and Actions, allowing the automation engine to
/// evaluate them in memory. The daily counter reset uses <c>ExecuteUpdateAsync</c> for bulk efficiency.</para>
///
/// <para><b>Purpose:</b>
/// Manages the full automation rule lifecycle: CRUD, webhook-time evaluation, and daily housekeeping.</para>
/// </summary>
public class AutomationRuleRepository(ApplicationDbContext db) : IAutomationRuleRepository
{
    /// <summary>
    /// Loads a rule with its full graph: Conditions, Actions (ordered by ExecutionOrder), and SocialAccount.
    /// Tracked — used for the rule edit flow and webhook-time evaluation.
    /// </summary>
    public async Task<AutomationRule?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await db.AutomationRules
            .Include(r => r.Conditions)
            .Include(r => r.Actions.OrderBy(a => a.ExecutionOrder))
            .Include(r => r.SocialAccount)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>
    /// Lists all rules for a workspace with SocialAccount loaded.
    /// AsNoTracking — the rules list page is read-only.
    /// </summary>
    public async Task<IReadOnlyList<AutomationRule>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.AutomationRules.AsNoTracking()
            .Include(r => r.SocialAccount)
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Critical webhook-time query: finds all enabled rules for a social account.
    /// Loads Conditions (for matching) and Actions (for execution) eagerly.
    /// Tracked — the automation engine increments TodayExecutionCount on successful triggers.
    /// Hits the composite index (SocialAccountId, IsEnabled) for efficient filtering.
    /// </summary>
    public async Task<IReadOnlyList<AutomationRule>> GetEnabledByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default)
        => await db.AutomationRules
            .Include(r => r.Conditions)
            .Include(r => r.Actions.OrderBy(a => a.ExecutionOrder))
            .Where(r => r.SocialAccountId == socialAccountId && r.IsEnabled)
            .ToListAsync(ct);

    /// <summary>
    /// Adds the rule (with Conditions and Actions as child entities) to the change tracker.
    /// EF Core inserts the entire aggregate in one transaction.
    /// </summary>
    public async Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken ct = default)
    {
        await db.AutomationRules.AddAsync(rule, ct);
        return rule;
    }

    /// <summary>
    /// Marks the rule as Modified for edits, toggle, or counter increment.
    /// </summary>
    public void Update(AutomationRule rule)
        => db.AutomationRules.Update(rule);

    /// <summary>
    /// Marks the rule for deletion. Cascade delete cleans up Conditions, Actions, and ExecutionLogs.
    /// </summary>
    public void Remove(AutomationRule rule)
        => db.AutomationRules.Remove(rule);

    /// <summary>
    /// Bulk UPDATE: resets TodayExecutionCount to 0 for all rules that fired today.
    /// Uses <c>ExecuteUpdateAsync</c> — a single SQL UPDATE without entity loading.
    /// Called by the midnight Hangfire job to prepare daily anti-spam counters.
    /// Only updates rules where the count is > 0 to minimize unnecessary writes.
    /// </summary>
    public async Task ResetDailyCountersAsync(CancellationToken ct = default)
        => await db.AutomationRules
            .Where(r => r.TodayExecutionCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TodayExecutionCount, 0), ct);
}
