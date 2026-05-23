using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="InboxMessage"/> entity.
///
/// <para><b>Role in the system:</b>
/// InboxMessage represents a single message within a conversation — either inbound (from an external
/// user via a webhook) or outbound (sent by a team member from the inbox UI). Messages can also be
/// automated (sent by an AutomationRule). This repository handles message storage, pagination for the
/// chat view, webhook deduplication, and bulk read-marking.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Inbox. Endpoints: GET/POST /api/inbox/conversations/{id}/messages.</para>
/// </summary>
public interface IInboxMessageRepository
{
    /// <summary>
    /// Retrieves one tracked inbox message by its identifier.
    /// </summary>
    Task<InboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists messages for a conversation with pagination, ordered chronologically (oldest first).
    /// Used to render the chat view — the frontend loads the latest 50 messages initially,
    /// then fetches older messages as the user scrolls up.
    /// Results are not tracked (read-only).
    /// </summary>
    /// <param name="conversationId">The conversation to fetch messages for.</param>
    /// <param name="skip">Number of messages to skip (for pagination).</param>
    /// <param name="take">Number of messages to return (page size, default 50).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A paginated list of <see cref="InboxMessage"/> entities.</returns>
    Task<IReadOnlyList<InboxMessage>> GetByConversationIdAsync(Guid conversationId, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Persists a new message — either inbound (from webhook) or outbound (from team member reply).
    /// After adding, the caller typically also updates the parent conversation's
    /// LastMessageAt, LastMessagePreview, and UnreadCount.
    /// </summary>
    Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Marks one message as modified for delivery-status or read-state updates.
    /// </summary>
    void Update(InboxMessage message);

    /// <summary>
    /// Removes one message when business rules allow deletion.
    /// </summary>
    void Remove(InboxMessage message);

    /// <summary>
    /// Checks if a message with the given external platform ID already exists.
    /// Used during webhook processing to prevent duplicate message insertion when
    /// the same webhook is delivered multiple times (platform retry logic).
    /// </summary>
    /// <param name="externalMessageId">The platform-side message identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the message already exists; <c>false</c> otherwise.</returns>
    Task<bool> ExistsByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one tracked message by its platform-side external identifier.
    /// </summary>
    Task<InboxMessage?> GetByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default);

    /// <summary>
    /// Marks all unread messages in a conversation as read by the team.
    /// Uses <c>ExecuteUpdateAsync</c> for bulk efficiency — sets IsReadByTeam = true
    /// and ReadAt = UtcNow on all matching messages in a single SQL UPDATE.
    /// Called when a team member opens a conversation in the inbox UI.
    /// </summary>
    Task MarkAsReadAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Counts messages that belong to one conversation.
    /// </summary>
    Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken ct = default);
}
