using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInboxConversationRepository"/>.
///
/// <para><b>How it works:</b>
/// Supports the Unified Inbox UI with dynamic filtering (platform, status, assignee).
/// The detail view eagerly loads messages ordered chronologically and the assignment navigation.
/// Webhook deduplication uses the composite unique index (SocialAccountId, ExternalConversationId).</para>
///
/// <para><b>Purpose:</b>
/// Powers the conversation list, thread view, and webhook processing for the unified inbox.</para>
/// </summary>
public class InboxConversationRepository(ApplicationDbContext db) : IInboxConversationRepository
{
    /// <inheritdoc />
    public async Task<InboxConversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.InboxConversations
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
                .ThenInclude(assignment => assignment!.AssignedTo)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, ct);

    /// <summary>
    /// Loads a conversation with all its messages (chronologically ordered), SocialAccount,
    /// and Assignment navigation. Tracked — the caller may update status or unread count.
    /// The <c>.OrderBy</c> inside the Include ensures messages render in chat order.
    /// </summary>
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
    /// Lists conversations with dynamic filters. Each filter is conditionally applied:
    /// - Platform filter traverses the SocialAccount navigation
    /// - Status filter checks the conversation's own Status enum
    /// - Assignee filter traverses the Assignment one-to-one navigation
    /// Ordered by LastMessageAt (newest first) for inbox-style display.
    /// AsNoTracking — the conversation list sidebar is read-only.
    /// </summary>
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<int> CountByWorkspaceIdAsync(
        Guid workspaceId,
        Platform? platform,
        ConversationStatus? status,
        Guid? assigneeUserId,
        bool unreadOnly,
        CancellationToken ct = default)
        => BuildFilteredQuery(workspaceId, platform, status, assigneeUserId, unreadOnly).CountAsync(ct);

    /// <inheritdoc />
    public Task<int> CountUnreadByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => db.InboxConversations.AsNoTracking()
            .CountAsync(conversation => conversation.WorkspaceId == workspaceId && conversation.UnreadCount > 0, ct);

    /// <inheritdoc />
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

    /// <inheritdoc />
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
    /// Looks up a conversation by its platform-side ID within a specific social account.
    /// Hits the composite unique index (SocialAccountId, ExternalConversationId).
    /// Used during webhook processing to find or create conversations.
    /// </summary>
    public async Task<InboxConversation?> GetByExternalIdAsync(Guid socialAccountId, string externalConversationId, CancellationToken ct = default)
        => await db.InboxConversations
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
            .FirstOrDefaultAsync(c =>
                c.SocialAccountId == socialAccountId &&
                c.ExternalConversationId == externalConversationId, ct);

    /// <inheritdoc />
    public Task<InboxConversation?> GetByExternalUserIdAsync(Guid socialAccountId, string externalUserId, CancellationToken ct = default)
        => db.InboxConversations
            .Include(conversation => conversation.SocialAccount)
            .Include(conversation => conversation.Assignment)
            .FirstOrDefaultAsync(
                conversation => conversation.SocialAccountId == socialAccountId &&
                                conversation.ExternalUserId == externalUserId,
                ct);

    /// <summary>
    /// Adds the conversation to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<InboxConversation> AddAsync(InboxConversation conversation, CancellationToken ct = default)
    {
        await db.InboxConversations.AddAsync(conversation, ct);
        return conversation;
    }

    /// <summary>
    /// Marks the conversation as Modified for status/unread/preview updates.
    /// </summary>
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
