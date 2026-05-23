namespace Application.DTOs.Webhook;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  WEBHOOK EVENT DTOs — Webhook Processing Pipeline Monitoring               ║
// ║  TRD Stage 4: Webhooks                                                     ║
// ║  Used by: Admin webhook monitor, debugging tools, health dashboard         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Webhook event record for display in the admin monitoring dashboard.
///
/// <para><b>Role in the system:</b>
/// WebhookEvent is the raw buffer for incoming platform notifications. The webhook endpoint
/// saves the raw JSON payload immediately (for speed — must respond 200 OK within 200ms),
/// then a Hangfire job processes events asynchronously. This DTO lets admins monitor the
/// webhook pipeline: see incoming event rate, processing status, verification failures,
/// and error details for failed events.</para>
///
/// <para><b>Where it's used:</b>
/// Admin-only "Webhook Monitor" page showing recent events with their processing status.
/// Useful for debugging integration issues (e.g., Instagram sends malformed payloads).</para>
///
/// <para><b>Security:</b>
/// RawPayload may contain sensitive platform data. Access to this DTO should be restricted
/// to workspace Owners/Admins. The payload is truncated in list views.</para>
/// </summary>
/// <param name="Id">The webhook event's unique identifier.</param>
/// <param name="Platform">Source platform: "YouTube", "Instagram", "Facebook", etc.</param>
/// <param name="EventType">Platform-defined event type (e.g., "comment.created", "message.received").</param>
/// <param name="Status">Processing status: "Received", "Processing", "Processed", "Failed".</param>
/// <param name="IsVerified">Whether the HMAC-SHA256 signature was verified. False = potentially spoofed.</param>
/// <param name="ReceivedAt">UTC timestamp when the webhook was received by our endpoint.</param>
/// <param name="ProcessedAt">UTC timestamp when processing completed, or null if still pending.</param>
/// <param name="ProcessingAttemptCount">Number of processing attempts (1 = first try, 2+ = retries after failure).</param>
/// <param name="ProcessingError">Error description if processing failed, or null on success.</param>
public record WebhookEventDto(
    Guid Id,
    string Platform,
    string EventType,
    string Status,
    bool IsVerified,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    int ProcessingAttemptCount,
    string? ProcessingError
);

/// <summary>
/// Full webhook event detail including the raw JSON payload.
///
/// <para><b>What it adds over <see cref="WebhookEventDto"/>:</b>
/// The complete raw JSON payload from the platform and the HMAC signature.
/// Used when an admin clicks into a specific event to debug processing failures.
/// The payload can be re-submitted for reprocessing from this view.</para>
///
/// <para><b>Security:</b>
/// This DTO should ONLY be accessible to workspace Owners. The raw payload may contain
/// personally identifiable information (PII) from external users.</para>
/// </summary>
/// <param name="Id">The webhook event's unique identifier.</param>
/// <param name="Platform">Source platform.</param>
/// <param name="EventType">Platform-defined event type.</param>
/// <param name="RawPayload">Complete JSON payload as received from the platform (for re-processing or debugging).</param>
/// <param name="Signature">HMAC-SHA256 signature sent by the platform, or null if not provided.</param>
/// <param name="IsVerified">Whether the signature verification passed.</param>
/// <param name="Status">Processing status as string.</param>
/// <param name="ReceivedAt">UTC timestamp when received.</param>
/// <param name="ProcessedAt">UTC timestamp when processed, or null.</param>
/// <param name="ProcessingAttemptCount">Number of processing attempts.</param>
/// <param name="ProcessingError">Error details, or null.</param>
public record WebhookEventDetailDto(
    Guid Id,
    string Platform,
    string EventType,
    string RawPayload,
    string? Signature,
    bool IsVerified,
    string Status,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    int ProcessingAttemptCount,
    string? ProcessingError
);
