using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="AutomationRule"/> aggregate root targeting the AutomationRules table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Manages the persistence of automation rules that drive the DM auto-reply engine. In Stage 4, rules are configured with triggers, conditions, and actions, which need to be stored and fetched efficiently to meet the performance goals of the auto-reply system.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Aggregates a rule with its child collections: TriggerConditions (Conditions table) and AutomationActions (Actions table). Operations include saving whole graphs and bulk updates.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Crucial for the automation system. Removing this repository would break the ability to configure, trigger, or audit auto-replies, violating core TRD requirements.</para>
/// </remarks>
public interface IAutomationRuleRepository
{
    /// <summary>
    /// Retrieves a rule with its Conditions and Actions eagerly loaded, plus the SocialAccount.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Necessary for presenting the full rule details on the edit screen and for evaluating matching rules during webhook processing.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL query with INNER/LEFT JOINs on Conditions, Actions, and SocialAccounts. Returns a tracked entity where Actions are ordered by ExecutionOrder.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Eager loading prevents lazy-loading performance issues and N+1 queries during trigger evaluation.</para>
    /// </remarks>
    Task<AutomationRule?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all automation rules for a workspace with their SocialAccount navigation.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Renders the workspace rules management dashboard page.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an AsNoTracking query filtered by WorkspaceId, ordered by CreatedAt descending, joined with SocialAccounts.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// AsNoTracking ensures optimal performance and lower memory usage for list queries.</para>
    /// </remarks>
    Task<IReadOnlyList<AutomationRule>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Gets all enabled rules for a specific social account, with Conditions and Actions loaded.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// The critical webhook-processing path. Finds all active rules that can be triggered by an incoming event.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Runs a tracked query on AutomationRules matching SocialAccountId and IsEnabled = true, eagerly loading Conditions and Actions.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Performance-critical; matches the composite index (SocialAccountId, IsEnabled) in the database for sub-millisecond lookups.</para>
    /// </remarks>
    Task<IReadOnlyList<AutomationRule>> GetEnabledByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new automation rule with its child Conditions and Actions.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows creation of new automation flows in the system.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds the entire rule aggregate graph (Conditions, Actions) to the tracking context in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Guarantees atomic transaction inserts of the rule and its nested configurations.</para>
    /// </remarks>
    Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken ct = default);

    /// <summary>
    /// Marks a rule as modified.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required when updating rule configurations, toggling status, or incrementing execution counters.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the rule entity as Modified in the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures proper EF Core tracking transitions for existing rules.</para>
    /// </remarks>
    void Update(AutomationRule rule);

    /// <summary>
    /// Permanently removes an automation rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows deletion of unwanted or outdated automation configurations.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the rule for deletion. Cascade delete cleans up all dependent Conditions, Actions, and execution history.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents orphaned records in child tables by executing correct cascading deletes at the database layer.</para>
    /// </remarks>
    void Remove(AutomationRule rule);

    /// <summary>
    /// Resets TodayExecutionCount to 0 for all rules.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enforces daily anti-spam execution quotas. Resets trigger counts at midnight.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a bulk UPDATE in SQL using ExecuteUpdateAsync, bypassing tracking for performance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// High-performance daily cleanup job that scales independently of the total rule count in the system.</para>
    /// </remarks>
    Task ResetDailyCountersAsync(CancellationToken ct = default);
}
