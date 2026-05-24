using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInboxConversationRepository"/> targeting the InboxConversations table.
/// </summary>
/// <remarks>
/// <para><b>How it works:</b>
/// Interfaces with <see cref="ApplicationDbContext"/>, utilizing eager loading for conversation detail queries and `.AsNoTracking()` for read-only listings.</para>
/// <para><b>Purpose:</b>
/// Powers the unified support inbox UI and handles incoming webhook message routing.</para>
/// </remarks>
public class InboxConversationRepository(ApplicationDbContext db) : IInboxConversationRepository
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
    public async Task<InboxConversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.InboxConversations
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
                .ThenInclude(assignment => assignment!.AssignedTo)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, ct);

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
    public async Task<InboxConversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken ct = default)
        => await db.InboxConversations
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .Include(c => c.SocialAccount)
            .Include(c => c.Assignment)
                .ThenInclude(assignment => assignment!.AssignedTo)
            .Include(c => c.Assignment)
                .ThenInclude(assignment => assignment!.AssignedBy)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

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
    public async Task<IReadOnlyList<InboxConversation>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform = null,
        ConversationStatus? status = null,
        Guid? assigneeUserId = null,
        CancellationToken ct = default)
    {
        var query = db.InboxConversations.AsNoTracking()
            .Include(c => c.SocialAccount)
            .Include(c => c.Assignment)
            .Where(c => c.WorkspaceId == workspaceId);

        if (platform.HasValue)
            query = query.Where(c => c.SocialAccount.Platform == platform.Value);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (assigneeUserId.HasValue)
            query = query.Where(c => c.Assignment != null && c.Assignment.AssignedToUserId == assigneeUserId.Value);

        return await query
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(ct);
    }

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
    public async Task<IReadOnlyList<InboxConversation>> GetPagedByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(workspaceId, platform, status, assigneeUserId, unreadOnly);

        return await query
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

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
    public Task<int> CountByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        CancellationToken ct = default)
        => BuildFilteredQuery(workspaceId, platform, status, assigneeUserId, unreadOnly).CountAsync(ct);

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
    public Task<int> CountUnreadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => db.InboxConversations.AsNoTracking()
            .CountAsync(conversation => conversation.WorkspaceId == workspaceId && conversation.UnreadCount > 0, ct);

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
    public async Task<IReadOnlyList<InboxConversation>> SearchByWorkspaceIdAsync(
        Guid workspaceId,
        string searchTerm,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = ApplySearch(
            BuildFilteredQuery(workspaceId, platform, status, assigneeUserId, unreadOnly),
            searchTerm);

        return await query
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

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
    public Task<int> CountSearchByWorkspaceIdAsync(
        Guid workspaceId,
        string searchTerm,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        CancellationToken ct = default)
        => ApplySearch(
                BuildFilteredQuery(workspaceId, platform, status, assigneeUserId, unreadOnly),
                searchTerm)
            .CountAsync(ct);

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
    public async Task<InboxConversation?> GetByExternalIdAsync(Guid socialAccountId, string externalConversationId, CancellationToken ct = default)
        => await db.InboxConversations.AsNoTracking()
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
            .FirstOrDefaultAsync(c =>
                c.SocialAccountId == socialAccountId &&
                c.ExternalConversationId == externalConversationId, ct);

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
    public Task<InboxConversation?> GetByExternalUserIdAsync(Guid socialAccountId, string externalUserId, CancellationToken ct = default)
        => db.InboxConversations.AsNoTracking()
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
            .FirstOrDefaultAsync(
                conversation => conversation.SocialAccountId == socialAccountId &&
                                conversation.ExternalUserId == externalUserId,
                ct);

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
    public async Task<InboxConversation> AddAsync(InboxConversation conversation, CancellationToken ct = default)
    {
        await db.InboxConversations.AddAsync(conversation, ct);
        return conversation;
    }

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
    public void Update(InboxConversation conversation)
        => db.InboxConversations.Update(conversation);

    private IQueryable<InboxConversation> BuildFilteredQuery(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly)
    {
        var query = db.InboxConversations.AsNoTracking()
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
                .ThenInclude(assignment => assignment!.AssignedTo)
            .Where(conversation => conversation.WorkspaceId == workspaceId);

        if (platform.HasValue)
        {
            query = query.Where(conversation => conversation.SocialAccount.Platform == platform.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(conversation => conversation.Status == status.Value);
        }

        if (assigneeUserId.HasValue)
        {
            query = query.Where(conversation =>
                conversation.Assignment != null &&
                conversation.Assignment.AssignedToUserId == assigneeUserId.Value);
        }

        if (unreadOnly)
        {
            query = query.Where(conversation => conversation.UnreadCount > 0);
        }

        return query;
    }

    private static IQueryable<InboxConversation> ApplySearch(IQueryable<InboxConversation> query, string searchTerm)
    {
        var normalized = searchTerm.Trim();
        return query.Where(conversation =>
            EF.Functions.ILike(conversation.ExternalUserName ?? string.Empty, $"%{normalized}%") ||
            EF.Functions.ILike(conversation.LastMessagePreview ?? string.Empty, $"%{normalized}%") ||
            conversation.Messages.Any(message => EF.Functions.ILike(message.TextContent ?? string.Empty, $"%{normalized}%")));
    }
}
