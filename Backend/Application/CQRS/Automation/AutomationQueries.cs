using Application.Common;
using Application.DTOs.Automation;
using Application.DTOs.PendingDM;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Automation;

/// <summary>
/// Retrieves a paginated list of automation rules for the current workspace.
/// </summary>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetAutomationRulesQuery(PagedRequest Pagination) : IRequest<Result<PagedResult<AutomationRuleDto>>>;

/// <summary>
/// Retrieves one automation rule with its full condition and action graph.
/// </summary>
/// <param name="RuleId">Rule that should be loaded.</param>
public sealed record GetAutomationRuleByIdQuery(Guid RuleId) : IRequest<Result<AutomationRuleDetailDto>>;

/// <summary>
/// Retrieves all condition types supported by the current automation engine.
/// </summary>
public sealed record GetAvailableConditionsQuery(Platform Platform, AutomationTriggerType TriggerType) : IRequest<Result<IReadOnlyList<AvailableConditionDto>>>;

/// <summary>
/// Retrieves paginated pending DM queue entries.
/// </summary>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetPendingDMsQuery(PendingDMStatus? Status, PagedRequest Pagination) : IRequest<Result<PagedResult<PendingDMQueueDto>>>;

/// <summary>
/// Retrieves paginated automation execution logs for a rule.
/// </summary>
/// <param name="RuleId">Rule whose execution history should be returned.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetExecutionLogsQuery(
    Guid RuleId,
    AutomationExecutionOutcome? Outcome,
    DateTime? From,
    DateTime? To,
    PagedRequest Pagination) : IRequest<Result<PagedResult<ExecutionLogDto>>>;

/// <summary>
/// Retrieves effectiveness statistics for one automation rule.
/// </summary>
/// <param name="RuleId">Rule whose effectiveness should be summarized.</param>
public sealed record GetRuleEffectivenessQuery(Guid RuleId) : IRequest<Result<AutomationRuleEffectivenessDto>>;
