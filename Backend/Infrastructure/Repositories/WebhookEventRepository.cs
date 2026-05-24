using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWebhookEventRepository"/> targeting the WebhookEvents table.
/// </summary>
/// <remarks>
/// <para><b>How it works:</b>
/// Interacts with <see cref="ApplicationDbContext"/> to write and read webhook event records, using `.AsNoTracking()` for read queries and change tracking for modifications.</para>
/// <para><b>Purpose:</b>
/// Provides the data-access operations required for ingestion and background processing of social platform events.</para>
/// </remarks>
public class WebhookEventRepository(ApplicationDbContext db) : IWebhookEventRepository
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
    public async Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
    {
        await db.WebhookEvents.AddAsync(webhookEvent, ct);
        return webhookEvent;
    }

    /// <summary>
    /// Retrieves one tracked webhook event by its identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows picking up a specific webhook event for processing or debugging.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an EF Core lookup with AsNoTracking for read-only access.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required for state transition checks during async processing runs.</para>
    /// </remarks>
    public Task<WebhookEvent?> GetByIdAsync(Guid webhookEventId, CancellationToken ct = default)
        => db.WebhookEvents.AsNoTracking().FirstOrDefaultAsync(webhookEvent => webhookEvent.Id == webhookEventId, ct);

    /// <summary>
    /// Retrieves a batch of verified but unprocessed webhook events for background processing.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives the background Hangfire parsing and processing job.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries verified events in Received status with AsNoTracking, sorted chronologically (FIFO), limited by batch size.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Eliminates concurrency conflicts by retrieving items sequentially in batches.</para>
    /// </remarks>
    public async Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int batchSize = 100, CancellationToken ct = default)
        => await db.WebhookEvents.AsNoTracking()
            .Where(we => we.Status == WebhookEventStatus.Received && we.IsVerified)
            .OrderBy(we => we.ReceivedAt)
            .Take(batchSize)
            .ToListAsync(ct);

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
    public async Task<IReadOnlyList<WebhookEvent>> GetFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(status, platform, from);
        return await query
            .OrderByDescending(webhookEvent => webhookEvent.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

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
    public Task<int> CountFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        CancellationToken ct = default)
        => BuildFilteredQuery(status, platform, from).CountAsync(ct);

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
    public void Update(WebhookEvent webhookEvent)
        => db.WebhookEvents.Update(webhookEvent);

    private IQueryable<WebhookEvent> BuildFilteredQuery(WebhookEventStatus? status, Platform? platform, DateTime? from)
    {
        var query = db.WebhookEvents.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(webhookEvent => webhookEvent.Status == status.Value);
        }

        if (platform.HasValue)
        {
            query = query.Where(webhookEvent => webhookEvent.Platform == platform.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(webhookEvent => webhookEvent.ReceivedAt >= from.Value);
        }

        return query;
    }
}
