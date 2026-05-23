using Application.Common;
using MediatR;

namespace Application.CQRS.Webhooks;

/// <summary>
/// Persists a received webhook event for later asynchronous processing.
/// </summary>
/// <param name="Platform">Source platform name.</param>
/// <param name="EventType">Platform-specific event type name.</param>
/// <param name="RawPayload">Raw request payload as received from the platform.</param>
/// <param name="Signature">Optional signature header value used for verification.</param>
public sealed record ReceiveWebhookCommand(
    string Platform,
    string EventType,
    string RawPayload,
    string? Signature) : IRequest<Result<Guid>>;

/// <summary>
/// Processes a previously stored webhook event.
/// </summary>
/// <param name="WebhookEventId">Webhook event that should be processed.</param>
public sealed record ProcessWebhookCommand(Guid WebhookEventId) : IRequest<Result>;

/// <summary>
/// Re-runs processing for a webhook event that previously failed or needs replay.
/// </summary>
/// <param name="WebhookEventId">Webhook event that should be reprocessed.</param>
public sealed record ReprocessWebhookEventCommand(Guid WebhookEventId) : IRequest<Result>;
