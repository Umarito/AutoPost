using Application.Common;
using Application.DTOs.Inbox;
using MediatR;

namespace Application.CQRS.Inbox;

/// <summary>
/// Retrieves a paginated list of conversations for the workspace inbox.
/// </summary>
/// <param name="Filter">Filter set for platform, status, assignee and unread state.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetConversationsPagedQuery(InboxFilterRequest Filter, PagedRequest Pagination) : IRequest<Result<PagedResult<ConversationSummaryDto>>>;

/// <summary>
/// Retrieves one conversation with full message history and assignment context.
/// </summary>
/// <param name="ConversationId">Conversation that should be loaded.</param>
public sealed record GetConversationDetailQuery(Guid ConversationId) : IRequest<Result<ConversationDetailDto>>;

/// <summary>
/// Retrieves the current unread conversation count for the workspace or current user view.
/// </summary>
public sealed record GetUnreadCountQuery() : IRequest<Result<int>>;

/// <summary>
/// Searches inbox conversations using the supplied filter and search term.
/// </summary>
/// <param name="Filter">Search and filter payload.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record SearchConversationsQuery(InboxFilterRequest Filter, PagedRequest Pagination) : IRequest<Result<PagedResult<ConversationSearchResultDto>>>;

/// <summary>
/// Retrieves paginated messages for one conversation.
/// </summary>
/// <param name="ConversationId">Conversation whose messages should be listed.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetMessagesPagedQuery(Guid ConversationId, PagedRequest Pagination) : IRequest<Result<PagedResult<MessageDto>>>;

/// <summary>
/// Retrieves team workload information for inbox assignment balancing.
/// </summary>
public sealed record GetTeamWorkloadQuery() : IRequest<Result<IReadOnlyList<TeamWorkloadDto>>>;
