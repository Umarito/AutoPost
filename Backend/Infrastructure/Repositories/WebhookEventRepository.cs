using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWebhookEventRepository"/>.
///
/// <para><b>How it works:</b>
/// The webhook controller calls <c>AddAsync</c> to save the raw payload as fast as possible,
/// then responds 200 OK. A background Hangfire job calls <c>GetPendingAsync</c> to pick up
/// verified events in FIFO order and process them (parse, match rules, execute actions).</para>
///
/// <para><b>Purpose:</b>
/// Implements the "save immediately, process later" pattern that ensures webhook endpoints
/// respond within the platform's timeout window (typically 200ms).</para>
/// </summary>
public class WebhookEventRepository(ApplicationDbContext db) : IWebhookEventRepository
{
    /// <summary>
    /// Adds the event to the change tracker for immediate persistence.
    /// This must be followed by SaveChangesAsync before returning the 200 OK response.
    /// </summary>
    public async Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
    {
        await db.WebhookEvents.AddAsync(webhookEvent, ct);
        return webhookEvent;
    }

    /// <inheritdoc />
    public Task<WebhookEvent?> GetByIdAsync(Guid webhookEventId, CancellationToken ct = default)
        => db.WebhookEvents.FirstOrDefaultAsync(webhookEvent => webhookEvent.Id == webhookEventId, ct);

    /// <summary>
    /// Retrieves a batch of verified, unprocessed events in FIFO order.
    /// Only returns events where Status == Received AND IsVerified == true
    /// (events that failed HMAC-SHA256 verification are excluded).
    /// Tracked — the processor updates Status to Processing, then Processed or Failed.
    /// Hits the composite index (Status, ReceivedAt) for efficient batch retrieval.
    /// </summary>
    public async Task<IReadOnlyList<WebhookEvent>> GetPendingAsync(int batchSize = 100, CancellationToken ct = default)
        => await db.WebhookEvents
            .Where(we => we.Status == WebhookEventStatus.Received && we.IsVerified)
            .OrderBy(we => we.ReceivedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<int> CountFilteredAsync(
        WebhookEventStatus? status,
        Platform? platform,
        DateTime? from,
        CancellationToken ct = default)
        => BuildFilteredQuery(status, platform, from).CountAsync(ct);

    /// <summary>
    /// Marks the event as Modified for status transitions and error recording.
    /// </summary>
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
