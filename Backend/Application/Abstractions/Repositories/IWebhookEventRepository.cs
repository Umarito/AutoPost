using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="WebhookEvent"/> entity targeting the WebhookEvents table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// Implements immediate logging and queue buffering for webhook requests from third-party social integrations. Essential to prevent timeouts on webhook verification calls from platforms like Meta or Google which demand responses within 200-300ms.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Stores raw event payloads in SQL as received. A separate background processor parses and matches them against automation rules.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Prevents message drops and webhook ingestion failures during network latency peaks or high load.</para>
/// </remarks>
public interface IWebhookEventRepository
{
    /// <summary>
    /// Persists a new webhook event immediately on arrival.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Saves raw payload as fast as possible to unblock the webhook receiver endpoint.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds a new <see cref="WebhookEvent"/> record to the change tracker in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Necessary for the 200ms quick-respond-first pattern.</para>
    /// </remarks>
    Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one tracked webhook event by its identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows picking up a specific webhook event for processing or debugging.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an EF Core lookup on WebhookEvents, returning a tracked instance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required for state transition checks during async processing runs.</para>
    /// </remarks>
    Task<WebhookEvent?> GetByIdAsync(Guid webhookEventId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a batch of verified but unprocessed webhook events for background processing.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives the background Hangfire parsing and processing job.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries verified events in Received status, sorted chronologically (FIFO), limited by batch size. Returns tracked results.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Eliminates concurrency conflicts by retrieving items sequentially in batches.</para>
    /// </remarks>
    Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one filtered page of webhook events for monitoring and replay tooling.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives webhook execution logs screens for developers and workspace admins.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Applies status, platform, and date filters, sorting newest first with Skip/Take paging.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Crucial for operational monitoring and debug analysis of integrations.</para>
    /// </remarks>
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
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Displays page boundaries inside the system monitoring dashboard.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs a SQL COUNT query using filters from <see cref="GetFilteredAsync"/>.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Provides paging controls with efficient database execution.</para>
    /// </remarks>
    Task<int> CountFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a webhook event as modified.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required when updating event status (Received -> Processing -> Processed/Failed).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the entity state as Modified in the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures status updates are saved to database after processing completes.</para>
    /// </remarks>
    void Update(WebhookEvent webhookEvent);
}
