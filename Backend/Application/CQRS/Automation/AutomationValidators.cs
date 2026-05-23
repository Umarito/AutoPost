using FluentValidation;
using Domain.Enums;

namespace Application.CQRS.Automation;

/// <summary>
/// Validates automation rule creation requests.
/// </summary>
public sealed class CreateAutomationRuleCommandValidator : AbstractValidator<CreateAutomationRuleCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CreateAutomationRuleCommand"/>.
    /// </summary>
    public CreateAutomationRuleCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Request.SocialAccountId).NotEmpty();
        RuleFor(x => x.Request.Conditions).NotNull().Must(items => items.Count > 0);
        RuleFor(x => x.Request.Actions).NotNull().Must(items => items.Count > 0);
    }
}

/// <summary>
/// Validates automation rule update requests.
/// </summary>
public sealed class UpdateAutomationRuleCommandValidator : AbstractValidator<UpdateAutomationRuleCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateAutomationRuleCommand"/>.
    /// </summary>
    public UpdateAutomationRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Request.SocialAccountId).NotEmpty();
        RuleFor(x => x.Request.Conditions).NotNull().Must(items => items.Count > 0);
        RuleFor(x => x.Request.Actions).NotNull().Must(items => items.Count > 0);
    }
}

/// <summary>
/// Validates enable or disable requests for automation rules.
/// </summary>
public sealed class ToggleAutomationRuleCommandValidator : AbstractValidator<ToggleAutomationRuleCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ToggleAutomationRuleCommand"/>.
    /// </summary>
    public ToggleAutomationRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}

/// <summary>
/// Validates automation rule deletion requests.
/// </summary>
public sealed class DeleteAutomationRuleCommandValidator : AbstractValidator<DeleteAutomationRuleCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DeleteAutomationRuleCommand"/>.
    /// </summary>
    public DeleteAutomationRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}

/// <summary>
/// Validates automation evaluation requests.
/// </summary>
public sealed class EvaluateAutomationRulesCommandValidator : AbstractValidator<EvaluateAutomationRulesCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="EvaluateAutomationRulesCommand"/>.
    /// </summary>
    public EvaluateAutomationRulesCommandValidator()
    {
        RuleFor(x => x.SocialAccountId).NotEmpty();
        RuleFor(x => x.TriggerType).NotEmpty();
        RuleFor(x => x.ExternalEventId).NotEmpty();
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}

/// <summary>
/// Validates pending DM enqueue requests.
/// </summary>
public sealed class EnqueuePendingDMCommandValidator : AbstractValidator<EnqueuePendingDMCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="EnqueuePendingDMCommand"/>.
    /// </summary>
    public EnqueuePendingDMCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.SocialAccountId).NotEmpty();
        RuleFor(x => x.ExternalUserId).NotEmpty();
        RuleFor(x => x.ResolvedMessageText).NotEmpty();
        RuleFor(x => x.ExternalUserName).MaximumLength(500).When(x => x.ExternalUserName is not null);
    }
}

/// <summary>
/// Validates pending DM cancellation requests.
/// </summary>
public sealed class CancelPendingDMCommandValidator : AbstractValidator<CancelPendingDMCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CancelPendingDMCommand"/>.
    /// </summary>
    public CancelPendingDMCommandValidator()
    {
        RuleFor(x => x.PendingDmId).NotEmpty();
    }
}

/// <summary>
/// Validates automation execution log write requests.
/// </summary>
public sealed class LogAutomationExecutionCommandValidator : AbstractValidator<LogAutomationExecutionCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="LogAutomationExecutionCommand"/>.
    /// </summary>
    public LogAutomationExecutionCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.ExternalTriggerEventId).NotEmpty();
        RuleFor(x => x.TriggerExternalUserId).NotEmpty();
    }
}

/// <summary>
/// Validates automation rule list queries.
/// </summary>
public sealed class GetAutomationRulesQueryValidator : AbstractValidator<GetAutomationRulesQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetAutomationRulesQuery"/>.
    /// </summary>
    public GetAutomationRulesQueryValidator()
    {
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates automation detail queries.
/// </summary>
public sealed class GetAutomationRuleByIdQueryValidator : AbstractValidator<GetAutomationRuleByIdQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetAutomationRuleByIdQuery"/>.
    /// </summary>
    public GetAutomationRuleByIdQueryValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}

/// <summary>
/// Validates pending DM list queries.
/// </summary>
public sealed class GetPendingDMsQueryValidator : AbstractValidator<GetPendingDMsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetPendingDMsQuery"/>.
    /// </summary>
    public GetPendingDMsQueryValidator()
    {
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates execution log list queries.
/// </summary>
public sealed class GetExecutionLogsQueryValidator : AbstractValidator<GetExecutionLogsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetExecutionLogsQuery"/>.
    /// </summary>
    public GetExecutionLogsQueryValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue);
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates available-condition discovery queries.
/// </summary>
public sealed class GetAvailableConditionsQueryValidator : AbstractValidator<GetAvailableConditionsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetAvailableConditionsQuery"/>.
    /// </summary>
    public GetAvailableConditionsQueryValidator()
    {
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.TriggerType).IsInEnum();
    }
}

/// <summary>
/// Validates automation effectiveness queries.
/// </summary>
public sealed class GetRuleEffectivenessQueryValidator : AbstractValidator<GetRuleEffectivenessQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetRuleEffectivenessQuery"/>.
    /// </summary>
    public GetRuleEffectivenessQueryValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}
