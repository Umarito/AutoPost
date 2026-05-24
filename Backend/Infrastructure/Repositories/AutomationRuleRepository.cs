using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRuleRepository"/> targeting the AutomationRules table.
/// </summary>
/// <remarks>
/// <para><b>How it works:</b>
/// Implements CRUD, daily reset jobs, and webhook trigger lookups using EF Core DbContext. Uses AsNoTracking for reads and change tracking for state changes.</para>
/// <para><b>Purpose:</b>
/// Encapsulates direct database operations for rules, conditions, actions, and limits.</para>
/// </remarks>
public class AutomationRuleRepository(ApplicationDbContext db) : IAutomationRuleRepository
{
    /// <summary>
    /// Loads a rule with its full graph: Conditions, Actions (ordered by ExecutionOrder), and SocialAccount.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Necessary for presenting the full rule details on the edit screen and for evaluating matching rules during webhook processing.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL query with INNER/LEFT JOINs on Conditions, Actions, and SocialAccounts. Returns a tracked entity where Actions are ordered by ExecutionOrder.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Eager loading prevents lazy-loading performance issues and N+1 queries during trigger evaluation.</para>
    /// </remarks>
    public async Task<AutomationRule?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await db.AutomationRules
            .Include(r => r.Conditions)
            .Include(r => r.Actions.OrderBy(a => a.ExecutionOrder))
            .Include(r => r.SocialAccount)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>
    /// Lists all rules for a workspace with SocialAccount loaded.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Renders the workspace rules management dashboard page.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an AsNoTracking query filtered by WorkspaceId, ordered by CreatedAt descending, joined with SocialAccounts.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// AsNoTracking ensures optimal performance and lower memory usage for list queries.</para>
    /// </remarks>
    public async Task<IReadOnlyList<AutomationRule>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.AutomationRules.AsNoTracking()
            .Include(r => r.SocialAccount)
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Critical webhook-time query: finds all enabled rules for a social account.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// The critical webhook-processing path. Finds all active rules that can be triggered by an incoming event.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Runs a read-only query with AsNoTracking on AutomationRules matching SocialAccountId and IsEnabled = true, eagerly loading Conditions and Actions.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Performance-critical; matches the composite index (SocialAccountId, IsEnabled) in the database for sub-millisecond lookups.</para>
    /// </remarks>
    public async Task<IReadOnlyList<AutomationRule>> GetEnabledByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default)
        => await db.AutomationRules.AsNoTracking()
            .Include(r => r.Conditions)
            .Include(r => r.Actions.OrderBy(a => a.ExecutionOrder))
            .Where(r => r.SocialAccountId == socialAccountId && r.IsEnabled)
            .ToListAsync(ct);

    /// <summary>
    /// Adds the rule to the change tracker.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows creation of new automation flows in the system.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds the entire rule aggregate graph (Conditions, Actions) to the tracking context in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Guarantees atomic transaction inserts of the rule and its nested configurations.</para>
    /// </remarks>
    public async Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken ct = default)
    {
        await db.AutomationRules.AddAsync(rule, ct);
        return rule;
    }

    /// <summary>
    /// Marks the rule as Modified for edits, toggle, or counter increment.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required when updating rule configurations, toggling status, or incrementing execution counters.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the rule entity as Modified in the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures proper EF Core tracking transitions for existing rules.</para>
    /// </remarks>
    public void Update(AutomationRule rule)
        => db.AutomationRules.Update(rule);

    /// <summary>
    /// Marks the rule for deletion.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows deletion of unwanted or outdated automation configurations.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the rule for deletion. Cascade delete cleans up all dependent Conditions, Actions, and execution history.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents orphaned records in child tables by executing correct cascading deletes at the database layer.</para>
    /// </remarks>
    public void Remove(AutomationRule rule)
        => db.AutomationRules.Remove(rule);

    /// <summary>
    /// Bulk UPDATE: resets TodayExecutionCount to 0.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enforces daily anti-spam execution quotas. Resets trigger counts at midnight.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a bulk UPDATE in SQL using ExecuteUpdateAsync, bypassing tracking for performance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// High-performance daily cleanup job that scales independently of the total rule count in the system.</para>
    /// </remarks>
    public async Task ResetDailyCountersAsync(CancellationToken ct = default)
        => await db.AutomationRules
            .Where(r => r.TodayExecutionCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TodayExecutionCount, 0), ct);
}
