using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="AutomationRule"/> aggregate root.
///
/// <para><b>Role in the system:</b>
/// AutomationRule is the core of the DM Automation engine. Each rule follows a pattern:
/// Trigger (e.g., NewComment) → Condition(s) (e.g., text contains "price") → Action(s)
/// (e.g., SendDM with a link). This repository supports the full rule lifecycle: CRUD, toggling,
/// webhook-time rule evaluation, and daily counter resets by a scheduled Hangfire job.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Automation. Endpoints: GET/POST/PUT /api/automation/rules,
/// PUT .../rules/{id}/toggle, GET .../rules/{id}/logs.</para>
/// </summary>
public interface IAutomationRuleRepository
{
    /// <summary>
    /// Retrieves a rule with its Conditions and Actions eagerly loaded, plus the SocialAccount.
    /// Actions are ordered by ExecutionOrder so they execute in the correct sequence.
    /// Used for the rule detail/edit page and for webhook-time rule evaluation.
    /// The entity is tracked for updates.
    /// </summary>
    Task<AutomationRule?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all automation rules for a workspace with their SocialAccount navigation.
    /// Used to render the automation rules list page in the UI.
    /// Results are ordered by creation date (newest first) and are not tracked.
    /// </summary>
    Task<IReadOnlyList<AutomationRule>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Gets all enabled rules for a specific social account, with Conditions and Actions loaded.
    /// This is the critical query during webhook processing — when a webhook arrives, the system
    /// finds all active rules for the affected account and evaluates them against the event.
    /// Entities are tracked because the TodayExecutionCount may be incremented.
    /// </summary>
    Task<IReadOnlyList<AutomationRule>> GetEnabledByAccountIdAsync(Guid socialAccountId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new automation rule with its child Conditions and Actions.
    /// EF Core will insert all entities in the aggregate in a single transaction.
    /// </summary>
    Task<AutomationRule> AddAsync(AutomationRule rule, CancellationToken ct = default);

    /// <summary>
    /// Marks a rule as modified. Typical updates: editing conditions/actions, toggling IsEnabled,
    /// incrementing TodayExecutionCount after a successful trigger.
    /// </summary>
    void Update(AutomationRule rule);

    /// <summary>
    /// Permanently removes an automation rule. EF Core cascade delete will automatically clean up
    /// related TriggerConditions, AutomationActions, and AutomationExecutionLogs.
    /// </summary>
    void Remove(AutomationRule rule);

    /// <summary>
    /// Resets TodayExecutionCount to 0 for all rules that have been triggered today.
    /// Called by the midnight Hangfire job to prepare counters for the next day.
    /// Uses <c>ExecuteUpdateAsync</c> for bulk efficiency — no entity loading required.
    /// </summary>
    Task ResetDailyCountersAsync(CancellationToken ct = default);
}
