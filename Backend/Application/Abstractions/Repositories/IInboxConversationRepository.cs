using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="InboxConversation"/> aggregate root targeting the InboxConversations table.
/// </summary>
/// <remarks>
/// <para><b>Business &amp; Technical Justification:</b>
/// InboxConversation serves as the container for support threads across Instagram, YouTube, Facebook, and other platforms. The TRD demands a unified dashboard to display, filter, search, and manage chats in a multi-tenant setting.</para>
/// <para><b>Execution, Process &amp; Relationships:</b>
/// Relates to <see cref="SocialAccount"/> and <see cref="ConversationAssignment"/> one-to-one, and holds a collection of <see cref="InboxMessage"/> child entities.</para>
/// <para><b>Project Impact &amp; Indispensability:</b>
/// Core communication thread data holder. Removing it breaks the entire Inbox and manual chat replies system.</para>
/// </remarks>
public interface IInboxConversationRepository
{
    /// <summary>
    /// Retrieves one tracked conversation with social-account and assignment data for write-side operations.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Necessary to load a conversation when modifying status, reassigning, or marking read.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL query with LEFT JOINs on SocialAccounts and ConversationAssignments, returning a tracked instance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Assures that updates are tracked and correctly persisted inside unit of work transactions.</para>
    /// </remarks>
    Task<InboxConversation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a conversation with all its messages eagerly loaded (ordered chronologically).
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Necessary to display the complete chat history for an agent inside the Inbox UI.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries InboxConversations, including nested messages sorted by SentAt, SocialAccount details, and Assignment info.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Eagerly loading messages in a single database request avoids N+1 query patterns as the UI mounts.</para>
    /// </remarks>
    Task<InboxConversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists conversations for a workspace with optional filters.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Retrieves full conversation lists for background jobs or clean exports.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an AsNoTracking query filtered by WorkspaceId, Platform, Status, and Assignee.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Supports read-only scans across tenant conversations without tracker overhead.</para>
    /// </remarks>
    Task<IReadOnlyList<InboxConversation>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform = null,
        ConversationStatus? status = null,
        Guid? assigneeUserId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves one page of filtered conversations for the unified inbox list.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Drives the primary paginated list sidebar in the Inbox UI.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses AsNoTracking. Applies filters dynamically (Platform, Status, Assignee, UnreadOnly), ordering by LastMessageAt descending, applying Skip/Take offsets.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Allows support teams to navigate busy inbox threads efficiently without loading millions of messages.</para>
    /// </remarks>
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
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Calculates total count for paginating the Inbox list.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Executes a SQL COUNT query using the exact same filters as <see cref="GetPagedByWorkspaceIdAsync"/>.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Computes count efficiently in postgres, avoiding loading entity records into server memory.</para>
    /// </remarks>
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
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Displays the unread badge counter in the user interface navigation header.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs an AsNoTracking COUNT query checking for UnreadCount &gt; 0 and WorkspaceId matches.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Very frequent query; optimized to run over index on WorkspaceId and UnreadCount.</para>
    /// </remarks>
    Task<int> CountUnreadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Searches conversations by external user name, last message preview and message text.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Implements search boxes in the support interface to look up past customer tickets.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Uses ILike in SQL to perform text matching on ExternalUserName, LastMessagePreview, or message content text.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Indispensable for tracking past conversations by name or keyword.</para>
    /// </remarks>
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
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Required for search result pagination.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Performs COUNT using the search filters and search term.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Provides correct pagination boundaries for search requests.</para>
    /// </remarks>
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
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Used by webhook processors when a new message arrives from external APIs to associate it with an existing thread.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries InboxConversations table matching SocialAccountId and ExternalConversationId. Includes social account details.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Prevents creating duplicate threads for the same conversation when multiple webhooks arrive from the same user.</para>
    /// </remarks>
    Task<InboxConversation?> GetByExternalIdAsync(Guid socialAccountId, string externalConversationId, CancellationToken ct = default);

    /// <summary>
    /// Finds a conversation by the platform user identifier inside one connected social account.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Useful for mapping external user profiles to internal conversation states.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Queries matching SocialAccountId and ExternalUserId, including assignment info.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Allows mapping platform-specific users to threads when conversation IDs differ.</para>
    /// </remarks>
    Task<InboxConversation?> GetByExternalUserIdAsync(Guid socialAccountId, string externalUserId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new conversation.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Created when a webhook introduces a message from a new contact.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Inserts a new tracked <see cref="InboxConversation"/> entity into the change tracker.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Required to register new conversations in the system.</para>
    /// </remarks>
    Task<InboxConversation> AddAsync(InboxConversation conversation, CancellationToken ct = default);

    /// <summary>
    /// Marks a conversation as modified.
    /// </summary>
    /// <remarks>
    /// <para><b>Business &amp; Technical Justification:</b>
    /// Updates unread counters, last message previews, or workflows.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b>
    /// Updates the entity state in EF Core tracker to Modified.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b>
    /// Ensures changes are flushed during Unit of Work transactions.</para>
    /// </remarks>
    void Update(InboxConversation conversation);
}
