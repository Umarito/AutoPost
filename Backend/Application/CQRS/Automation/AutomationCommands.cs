using Application.Common;
using Application.DTOs.Automation;
using Application.DTOs.PendingDM;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Automation;

/// <summary>
/// Creates a new automation rule with its conditions and actions.
/// </summary>
/// <param name="Request">Automation rule creation payload.</param>
public sealed record CreateAutomationRuleCommand(CreateAutomationRuleRequest Request) : IRequest<Result<AutomationRuleDetailDto>>;

/// <summary>
/// Updates an existing automation rule.
/// </summary>
/// <param name="RuleId">Rule that should be updated.</param>
/// <param name="Request">Updated automation rule definition.</param>
public sealed record UpdateAutomationRuleCommand(Guid RuleId, CreateAutomationRuleRequest Request) : IRequest<Result<AutomationRuleDetailDto>>;

/// <summary>
/// Enables or disables an automation rule.
/// </summary>
/// <param name="RuleId">Rule whose enabled state should change.</param>
/// <param name="IsEnabled">Desired enabled state.</param>
public sealed record ToggleAutomationRuleCommand(Guid RuleId, bool IsEnabled) : IRequest<Result>;

/// <summary>
/// Deletes an automation rule and its dependent configuration.
/// </summary>
/// <param name="RuleId">Rule that should be removed.</param>
public sealed record DeleteAutomationRuleCommand(Guid RuleId) : IRequest<Result>;

/// <summary>
/// Evaluates all matching automation rules for a specific external event.
/// </summary>
/// <param name="SocialAccountId">Social account that received the trigger event.</param>
/// <param name="TriggerType">Trigger type name reported by the event source.</param>
/// <param name="ExternalEventId">External event identifier used for idempotency.</param>
/// <param name="PayloadJson">Raw normalized event payload that rules will evaluate.</param>
public sealed record EvaluateAutomationRulesCommand(
    Guid SocialAccountId,
    string TriggerType,
    string ExternalEventId,
    string PayloadJson) : IRequest<Result>;

/// <summary>
/// Enqueues a deferred DM action for later processing.
/// </summary>
/// <param name="RuleId">Rule that generated the queued DM.</param>
/// <param name="SocialAccountId">Connected account that will eventually send the DM.</param>
/// <param name="ExternalUserId">Platform-side target user identifier.</param>
/// <param name="ResolvedMessageText">Fully rendered DM text ready for delivery.</param>
public sealed record EnqueuePendingDMCommand(
    Guid RuleId,
    Guid SocialAccountId,
    string ExternalUserId,
    string ResolvedMessageText,
    PendingReason Reason,
    string? ExternalUserName = null,
    DateTime? ExpiresAtUtc = null) : IRequest<Result>;

/// <summary>
/// Processes pending DM queue entries that may now be eligible for delivery.
/// </summary>
public sealed record ProcessPendingDMsCommand() : IRequest<Result>;

/// <summary>
/// Cancels a queued pending DM entry.
/// </summary>
/// <param name="PendingDmId">Queue entry that should be cancelled.</param>
public sealed record CancelPendingDMCommand(Guid PendingDmId) : IRequest<Result>;

/// <summary>
/// Writes an automation execution log entry.
/// </summary>
/// <param name="RuleId">Rule whose execution should be logged.</param>
/// <param name="Outcome">Outcome value as string.</param>
/// <param name="TriggerUserName">Display name of the external user that triggered the rule.</param>
/// <param name="TriggerContent">Optional content that caused the trigger.</param>
/// <param name="SkipReason">Optional reason why the execution was skipped.</param>
public sealed record LogAutomationExecutionCommand(
    Guid RuleId,
    string ExternalTriggerEventId,
    string TriggerExternalUserId,
    AutomationExecutionOutcome Outcome,
    string? TriggerUserName,
    string? TriggerContent,
    string? SkipReason,
    Guid? PendingDMQueueId,
    string? ErrorMessage) : IRequest<Result>;
