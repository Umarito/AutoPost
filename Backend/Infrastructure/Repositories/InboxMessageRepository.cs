using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInboxMessageRepository"/>.
///
/// <para><b>How it works:</b>
/// Supports paginated message loading for the chat view, webhook deduplication via
/// ExternalMessageId index, and bulk mark-as-read using <c>ExecuteUpdateAsync</c>.</para>
///
/// <para><b>Purpose:</b>
/// Handles individual messages within inbox conversations — both inbound (from webhooks)
/// and outbound (from team replies).</para>
/// </summary>
public class InboxMessageRepository(ApplicationDbContext db) : IInboxMessageRepository
{
    /// <inheritdoc />
    public Task<InboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.InboxMessages
            .Include(message => message.SentBy)
            .Include(message => message.AutomationRule)
            .FirstOrDefaultAsync(message => message.Id == id, ct);

    /// <summary>
    /// Loads messages for a conversation with pagination. Ordered by SentAt (oldest first)
    /// so the chat reads top-to-bottom chronologically. Uses Skip/Take for cursor-based
    /// pagination — the frontend requests older messages as the user scrolls up.
    /// AsNoTracking — the message list is read-only.
    /// </summary>
    public async Task<IReadOnlyList<InboxMessage>> GetByConversationIdAsync(
        Guid conversationId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await db.InboxMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>
    /// Adds the message to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken ct = default)
    {
        await db.InboxMessages.AddAsync(message, ct);
        return message;
    }

    /// <inheritdoc />
    public void Update(InboxMessage message)
        => db.InboxMessages.Update(message);

    /// <inheritdoc />
    public void Remove(InboxMessage message)
        => db.InboxMessages.Remove(message);

    /// <summary>
    /// Uses AnyAsync (SELECT EXISTS) against the ExternalMessageId index.
    /// Called during webhook processing to skip duplicate message deliveries
    /// caused by platform retry logic.
    /// </summary>
    public async Task<bool> ExistsByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default)
        => await db.InboxMessages.AsNoTracking()
            .AnyAsync(m => m.ExternalMessageId == externalMessageId, ct);

    /// <inheritdoc />
    public Task<InboxMessage?> GetByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default)
        => db.InboxMessages
            .Include(message => message.Conversation)
            .FirstOrDefaultAsync(message => message.ExternalMessageId == externalMessageId, ct);

    /// <summary>
    /// Bulk UPDATE: marks all unread messages in a conversation as read.
    /// Uses <c>ExecuteUpdateAsync</c> — a single SQL UPDATE without loading entities:
    /// UPDATE InboxMessages SET IsReadByTeam = true, ReadAt = @now
    /// WHERE ConversationId = @id AND IsReadByTeam = false.
    /// Called when a team member opens a conversation.
    /// </summary>
    public async Task MarkAsReadAsync(Guid conversationId, CancellationToken ct = default)
        => await db.InboxMessages
            .Where(m => m.ConversationId == conversationId && !m.IsReadByTeam)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsReadByTeam, true)
                .SetProperty(m => m.ReadAt, DateTime.UtcNow), ct);

    /// <inheritdoc />
    public Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
        => db.InboxMessages.AsNoTracking()
            .CountAsync(message => message.ConversationId == conversationId, ct);
}
