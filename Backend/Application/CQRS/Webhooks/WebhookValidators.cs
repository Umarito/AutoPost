using FluentValidation;

namespace Application.CQRS.Webhooks;

/// <summary>
/// Validates received webhook payloads before they are persisted.
/// </summary>
public sealed class ReceiveWebhookCommandValidator : AbstractValidator<ReceiveWebhookCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ReceiveWebhookCommand"/>.
    /// </summary>
    public ReceiveWebhookCommandValidator()
    {
        RuleFor(x => x.Platform).NotEmpty();
        RuleFor(x => x.EventType).NotEmpty();
        RuleFor(x => x.RawPayload).NotEmpty();
    }
}

/// <summary>
/// Validates webhook processing requests.
/// </summary>
public sealed class ProcessWebhookCommandValidator : AbstractValidator<ProcessWebhookCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ProcessWebhookCommand"/>.
    /// </summary>
    public ProcessWebhookCommandValidator()
    {
        RuleFor(x => x.WebhookEventId).NotEmpty();
    }
}

/// <summary>
/// Validates webhook replay requests.
/// </summary>
public sealed class ReprocessWebhookEventCommandValidator : AbstractValidator<ReprocessWebhookEventCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ReprocessWebhookEventCommand"/>.
    /// </summary>
    public ReprocessWebhookEventCommandValidator()
    {
        RuleFor(x => x.WebhookEventId).NotEmpty();
    }
}

/// <summary>
/// Validates webhook verification challenge queries.
/// </summary>
public sealed class VerifyWebhookSubscriptionQueryValidator : AbstractValidator<VerifyWebhookSubscriptionQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="VerifyWebhookSubscriptionQuery"/>.
    /// </summary>
    public VerifyWebhookSubscriptionQueryValidator()
    {
        RuleFor(x => x.Platform).NotEmpty();
        RuleFor(x => x.Challenge).NotEmpty();
    }
}

/// <summary>
/// Validates webhook event list queries.
/// </summary>
public sealed class GetWebhookEventsQueryValidator : AbstractValidator<GetWebhookEventsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetWebhookEventsQuery"/>.
    /// </summary>
    public GetWebhookEventsQueryValidator()
    {
        RuleFor(x => x.From)
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
            .When(x => x.From.HasValue);
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
