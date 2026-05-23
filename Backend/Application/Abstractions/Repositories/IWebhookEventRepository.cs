using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="WebhookEvent"/> infrastructure entity.
///
/// <para><b>Role in the system:</b>
/// WebhookEvent is a buffer table for incoming webhook events from social platforms (Instagram,
/// YouTube, etc.). The critical design constraint is speed: the webhook endpoint must respond
/// with 200 OK within 200ms, so it saves the raw JSON payload immediately and returns.
/// A background Hangfire job then picks up unprocessed events and handles them.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Webhooks. Endpoint: POST /api/webhooks/{platform}.
/// "Save RawPayload immediately → respond 200 OK → process in background."</para>
/// </summary>
public interface IWebhookEventRepository
{
    /// <summary>
    /// Persists a new webhook event immediately on arrival.
    /// This is the fastest path — no validation, no processing, just save and return.
    /// Called from the webhook controller before responding 200 OK.
    /// </summary>
    Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one tracked webhook event by its identifier.
    /// </summary>
    Task<WebhookEvent?> GetByIdAsync(Guid webhookEventId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a batch of verified but unprocessed webhook events for background processing.
    /// Only returns events where Status == Received and IsVerified == true (HMAC-SHA256 passed).
    /// Events are ordered by arrival time (FIFO) and limited by batch size.
    /// Entities are tracked because the processor will update their status to Processing/Processed/Failed.
    /// </summary>
    /// <param name="batchSize">Maximum number of events to retrieve (default 100).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one filtered page of webhook events for monitoring and replay tooling.
    /// </summary>
    Task<IReadOnlyList<WebhookEvent>> GetFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts filtered webhook events for paged operational screens.
    /// </summary>
    Task<int> CountFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a webhook event as modified. Typical updates: status transitions
    /// (Received → Processing → Processed/Failed), incrementing ProcessingAttemptCount,
    /// recording ProcessedAt timestamp and ProcessingError message.
    /// </summary>
    void Update(WebhookEvent webhookEvent);
}
