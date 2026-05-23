using FluentValidation;

namespace Application.CQRS.Notifications;

/// <summary>
/// Validates single notification preference updates.
/// </summary>
public sealed class UpdateNotificationPreferenceCommandValidator : AbstractValidator<UpdateNotificationPreferenceCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateNotificationPreferenceCommand"/>.
    /// </summary>
    public UpdateNotificationPreferenceCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
    }
}

/// <summary>
/// Validates bulk notification preference updates.
/// </summary>
public sealed class UpdateAllNotificationPreferencesCommandValidator : AbstractValidator<UpdateAllNotificationPreferencesCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateAllNotificationPreferencesCommand"/>.
    /// </summary>
    public UpdateAllNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request)
            .Must(request =>
                !string.IsNullOrWhiteSpace(request.Preset) ||
                (request.Preferences is { Count: > 0 }))
            .WithMessage("Either a preset or at least one explicit preference item is required.");
    }
}

/// <summary>
/// Validates notification dispatch requests.
/// </summary>
public sealed class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="SendNotificationCommand"/>.
    /// </summary>
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

/// <summary>
/// Validates notification history queries.
/// </summary>
public sealed class GetNotificationHistoryQueryValidator : AbstractValidator<GetNotificationHistoryQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetNotificationHistoryQuery"/>.
    /// </summary>
    public GetNotificationHistoryQueryValidator()
    {
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
