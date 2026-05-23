using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="InboxConversation"/> aggregate root.
///
/// <para><b>Role in the system:</b>
/// InboxConversation represents a unified inbox thread between the team and an external user
/// across any connected social platform. It aggregates messages from Instagram DMs, YouTube comments,
/// Facebook Messenger, etc., into a single conversation view. Each conversation tracks the external
/// user's identity, unread count, and current status (Open, Resolved, Snoozed).</para>
///
/// <para><b>TRD reference:</b>
/// Stage 4 — Inbox &amp; Automation. Endpoints: GET /api/inbox/conversations,
/// GET/POST .../conversations/{id}/messages, PUT .../conversations/{id}/status.</para>
/// </summary>
public interface IInboxConversationRepository
{
    /// <summary>
    /// Retrieves one tracked conversation with social-account and assignment data for write-side operations.
    /// </summary>
    Task<InboxConversation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a conversation with all its messages eagerly loaded (ordered chronologically).
    /// Also loads the SocialAccount and Assignment navigations.
    /// Used to render the full conversation thread in the unified inbox.
    /// The entity is tracked for updates (status changes, unread count reset).
    /// </summary>
    Task<InboxConversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists conversations for a workspace with optional filters.
    /// Used to populate the conversation list sidebar in the inbox UI.
    /// Filters match TRD API: platform, status (Open/Resolved/Snoozed), and assignee.
    /// Results are ordered by last message time (newest first) and are not tracked.
    /// </summary>
    Task<IReadOnlyList<InboxConversation>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform = null,
        ConversationStatus? status = null,
        Guid? assigneeUserId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves one page of filtered conversations for the unified inbox list.
    /// </summary>
    Task<IReadOnlyList<InboxConversation>> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts conversations that match the supplied unified-inbox filters.
    /// </summary>
    Task<int> CountByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        CancellationToken ct = default);

    /// <summary>
    /// Counts unread conversations inside the current workspace.
    /// </summary>
    Task<int> CountUnreadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Searches conversations by external user name, last message preview and message text.
    /// </summary>
    Task<IReadOnlyList<InboxConversation>> SearchByWorkspaceIdAsync(
        Guid workspaceId,
        string searchTerm,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Counts search results for the unified inbox.
    /// </summary>
    Task<int> CountSearchByWorkspaceIdAsync(
        Guid workspaceId,
        string searchTerm,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        CancellationToken ct = default);

    /// <summary>
    /// Finds a conversation by its external platform-side ID within a specific social account.
    /// Called during webhook processing to check if an incoming message belongs to an existing
    /// conversation or should create a new one. Prevents duplicate conversations.
    /// </summary>
    Task<InboxConversation?> GetByExternalIdAsync(Guid socialAccountId, string externalConversationId, CancellationToken ct = default);

    /// <summary>
    /// Finds a conversation by the platform user identifier inside one connected social account.
    /// </summary>
    Task<InboxConversation?> GetByExternalUserIdAsync(Guid socialAccountId, string externalUserId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new conversation. Created automatically when a webhook introduces
    /// a new external user who hasn't chatted before.
    /// </summary>
    Task<InboxConversation> AddAsync(InboxConversation conversation, CancellationToken ct = default);

    /// <summary>
    /// Marks a conversation as modified. Typical updates: status change (Open → Resolved),
    /// updating unread count, refreshing LastMessageAt and LastMessagePreview.
    /// </summary>
    void Update(InboxConversation conversation);
}
