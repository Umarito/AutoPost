namespace Application.DTOs.Automation;

/// <summary>
/// Describes a condition type that can be used when building an automation rule.
/// </summary>
/// <param name="Type">Machine-readable condition type name.</param>
/// <param name="DisplayName">Human-readable name shown in the rule builder UI.</param>
/// <param name="Description">Short description of what the condition evaluates.</param>
public record AvailableConditionDto(
    string Type,
    string DisplayName,
    string Description);

/// <summary>
/// Summarizes how effective an automation rule has been over the selected period.
/// </summary>
/// <param name="RuleId">Rule the statistics belong to.</param>
/// <param name="TotalExecutions">Total number of trigger evaluations recorded for the rule.</param>
/// <param name="SuccessfulExecutions">Number of evaluations that resulted in an executed action.</param>
/// <param name="SkippedExecutions">Number of evaluations skipped because of limits, duplicates or conditions.</param>
/// <param name="FailedExecutions">Number of evaluations that ended in a technical or platform failure.</param>
public record AutomationRuleEffectivenessDto(
    Guid RuleId,
    int TotalExecutions,
    int SuccessfulExecutions,
    int SkippedExecutions,
    int FailedExecutions);
