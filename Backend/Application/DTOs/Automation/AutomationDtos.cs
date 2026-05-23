using System.ComponentModel.DataAnnotations;
using Application.DTOs.SocialAccount;
using Domain.Enums;

namespace Application.DTOs.Automation;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  AUTOMATION DTOs — Rules, Conditions, Actions, Execution Logs              ║
// ║  TRD Stage 4: Inbox & Automation (Extended)                                ║
// ║  Endpoints: GET/POST/PUT /api/automation/rules, .../toggle, .../logs       ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for creating an automation rule.
/// A rule follows: Trigger → Condition(s) → Action(s).
/// TRD API: POST /api/automation/rules
/// </summary>
/// <param name="Name">Human-readable rule name (e.g., "Send DM on new comment").</param>
/// <param name="Description">Optional detailed description of what the rule does.</param>
/// <param name="SocialAccountId">The social account this rule monitors for trigger events.</param>
/// <param name="TriggerType">Event that activates the rule: NewComment, NewFollower, NewDM, StoryMention.</param>
/// <param name="TargetExternalPostId">Optional: limit trigger to a specific post (by platform ID).</param>
/// <param name="MaxActionsPerUser">Max times this rule can fire for the same external user. Default 1 (anti-spam).</param>
/// <param name="DailyExecutionLimit">Max total executions per day, or null for unlimited.</param>
/// <param name="Conditions">At least one condition that must match for the rule to fire.</param>
/// <param name="Actions">At least one action to execute when conditions are met.</param>
public record CreateAutomationRuleRequest(
    [Required, MaxLength(300)] string Name,
    [MaxLength(2000)] string? Description,
    [Required] Guid SocialAccountId,
    [Required] AutomationTriggerType TriggerType,
    [MaxLength(200)] string? TargetExternalPostId,
    int MaxActionsPerUser = 1,
    int? DailyExecutionLimit = null,
    [Required, MinLength(1)] List<ConditionRequest> Conditions = default!,
    [Required, MinLength(1)] List<ActionRequest> Actions = default!
);

/// <summary>
/// Condition definition within a rule creation request.
/// Conditions are evaluated against the trigger event's data.
/// </summary>
/// <param name="Type">What to check: TextContains, TextEquals, UserFollowsUs, UserIsVerified.</param>
/// <param name="Operator">Comparison: Contains, Equals, NotContains, Regex.</param>
/// <param name="Value">The value to compare against, or null for boolean conditions (UserFollowsUs).</param>
/// <param name="IsCaseSensitive">Whether text comparison is case-sensitive. Default false.</param>
public record ConditionRequest(
    ConditionType Type,
    ConditionOperator Operator,
    string? Value,
    bool IsCaseSensitive = false
);

/// <summary>
/// Action definition within a rule creation request.
/// Actions execute sequentially by ExecutionOrder when all conditions match.
/// </summary>
/// <param name="Type">What to do: SendDM, ReplyComment, LikePost, FollowUser.</param>
/// <param name="ExecutionOrder">Sequence number (1, 2, 3...). Actions execute in this order.</param>
/// <param name="DelaySeconds">Delay before executing (anti-spam). Default 30 seconds.</param>
/// <param name="MessageTemplate">Message template with variables: {{username}}, {{post_url}}. Required for SendDM/ReplyComment.</param>
/// <param name="LinkUrl">URL to include in the message, or null.</param>
public record ActionRequest(
    ActionType Type,
    int ExecutionOrder,
    int DelaySeconds = 30,
    [MaxLength(2000)] string? MessageTemplate = null,
    [MaxLength(2048)] string? LinkUrl = null
);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Compact rule representation for the automation rules list page.
/// Shows: name, platform, trigger type, enabled state, daily execution stats.
/// </summary>
public record AutomationRuleDto(
    Guid Id, string Name, string Platform, string AccountDisplayName,
    string TriggerType, bool IsEnabled,
    int TodayExecutionCount, int? DailyExecutionLimit,
    DateTime CreatedAt
);

/// <summary>
/// Full rule representation with conditions, actions, and social account details.
/// Used for the rule detail/edit page.
/// </summary>
public record AutomationRuleDetailDto(
    Guid Id, string Name, string? Description, string Platform,
    SocialAccountDto SocialAccount, string TriggerType,
    string? TargetExternalPostId, int MaxActionsPerUser,
    int? DailyExecutionLimit, bool IsEnabled,
    List<ConditionDto> Conditions, List<ActionDto> Actions,
    DateTime CreatedAt
);

/// <summary>
/// Condition output for display in the rule detail view.
/// </summary>
public record ConditionDto(
    Guid Id, string Type, string Operator, string? Value, bool IsCaseSensitive
);

/// <summary>
/// Action output for display in the rule detail view.
/// </summary>
public record ActionDto(
    Guid Id, string Type, int ExecutionOrder, int DelaySeconds,
    string? MessageTemplate, string? LinkUrl
);

/// <summary>
/// Execution log entry showing what happened when a rule was triggered.
/// Outcome: "Executed" (success), "Skipped" (duplicate/limit), "Pending" (queued), "Failed" (API error).
/// </summary>
public record ExecutionLogDto(
    Guid Id,
    string ExternalTriggerEventId,
    string TriggerExternalUserId,
    string? TriggerUserName,
    string? TriggerContent,
    string Outcome,
    string? SkipReason,
    string? ErrorMessage,
    Guid? PendingDMQueueId,
    DateTime ExecutedAt
);
