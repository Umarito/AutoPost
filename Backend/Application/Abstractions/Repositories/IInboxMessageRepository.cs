using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="InboxMessage"/> entity targeting the InboxMessages table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// InboxMessage represents a single message inside a conversation (inbound or outbound, automated or manual). The TRD requires rendering these chronologically in the Unified Inbox UI.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Relates to <see cref="InboxConversation"/>, <see cref="ApplicationUser"/> (sender, if outbound/manual), and <see cref="AutomationRule"/> (if automated).</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Stores the actual chat messages. Removing this contract disables all direct messaging logs and support response features.</para>
/// </remarks>
public interface IInboxMessageRepository
{
    /// <summary>
    /// Retrieves one tracked inbox message by its identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows inspecting or updating individual message delivery status.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL query with INNER/LEFT JOINs on SentBy and AutomationRule, returning a tracked instance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required for updating delivery status when external webhooks return asynchronously.</para>
    /// </remarks>
    Task<InboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists messages for a conversation with pagination, ordered chronologically (oldest first).
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Powers the scrollable message grid inside the Inbox support chat UI.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Filters by ConversationId, sorting by SentAt ascending, and applies Skip/Take paging offsets.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures that only a small window of messages is loaded at a time, avoiding high bandwidth usage.</para>
    /// </remarks>
    Task<IReadOnlyList<InboxMessage>> GetByConversationIdAsync(Guid conversationId, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>
    /// Persists a new message.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Used when a new message is received via webhook or sent by an agent/automation rule.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Adds a new <see cref="InboxMessage"/> instance to the change tracker in the Added state.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Essential to store message content inside the DB.</para>
    /// </remarks>
    Task<InboxMessage> AddAsync(InboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Marks one message as modified.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Enables delivery status or read state updates.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Sets the entity state to Modified in the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required to persist status flags.</para>
    /// </remarks>
    void Update(InboxMessage message);

    /// <summary>
    /// Removes one message when business rules allow deletion.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Allows deleting outbound messages that are pending delivery.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Marks the tracked message for deletion from the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required to clean up unwanted local messages.</para>
    /// </remarks>
    void Remove(InboxMessage message);

    /// <summary>
    /// Checks if a message with the given external platform ID already exists.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Serves as the webhook message deduplication guard.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries the InboxMessages table using AsNoTracking and AnyAsync matching ExternalMessageId.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents duplicate messages from rendering in the chat UI when webhook retries occur.</para>
    /// </remarks>
    Task<bool> ExistsByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves one tracked message by its platform-side external identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Used when a status update callback arrives via webhook with only an external message ID.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries the database including parent Conversation, returning a tracked instance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required for write-side delivery state transitions.</para>
    /// </remarks>
    Task<InboxMessage?> GetByExternalMessageIdAsync(string externalMessageId, CancellationToken ct = default);

    /// <summary>
    /// Marks all unread messages in a conversation as read by the team.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Updates message read status in bulk when an agent opens a conversation.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a bulk UPDATE in SQL using ExecuteUpdateAsync, modifying IsReadByTeam and ReadAt columns.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Highly efficient database modification that scales independently of the message count in the conversation.</para>
    /// </remarks>
    Task MarkAsReadAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// Counts messages that belong to one conversation.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required for page calculations or UI analytics.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an AsNoTracking COUNT query filtered by ConversationId.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Avoids loading message lists just to retrieve total count.</para>
    /// </remarks>
    Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken ct = default);
}
