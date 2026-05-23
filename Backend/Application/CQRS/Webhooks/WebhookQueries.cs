using Application.Common;
using Application.DTOs.Webhook;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Webhooks;

/// <summary>
/// Validates a provider's webhook subscription challenge request.
/// </summary>
/// <param name="Platform">Source platform requesting verification.</param>
/// <param name="Challenge">Challenge value that should be echoed or transformed.</param>
/// <param name="Token">Verification token presented by the provider.</param>
public sealed record VerifyWebhookSubscriptionQuery(string Platform, string Challenge, string? Token) : IRequest<Result<string>>;

/// <summary>
/// Retrieves paginated webhook events for operational monitoring.
/// </summary>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetWebhookEventsQuery(
    WebhookEventStatus? Status,
    Platform? Platform,
    DateTime? From,
    PagedRequest Pagination) : IRequest<Result<PagedResult<WebhookEventDto>>>;
