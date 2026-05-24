using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Inbox;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Inbox;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  INBOX COMMAND HANDLERS                                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Handles creating a new conversation or updating an existing one from an external event source.
/// Called during webhook processing when a new message arrives from a platform.
/// </summary>
public sealed class CreateOrUpdateConversationCommandHandler
    : IRequestHandler<CreateOrUpdateConversationCommand, Result<ConversationDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrUpdateConversationCommandHandler"/> class.
    /// </summary>
    public CreateOrUpdateConversationCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<ConversationDetailDto>> Handle(
        CreateOrUpdateConversationCommand request,
        CancellationToken cancellationToken)
    {
        // Verify the social account exists and belongs to the current workspace (IDOR check).
        var socialAccount = await _socialAccountRepository.GetByIdWithWorkspaceAsync(
            request.SocialAccountId, cancellationToken);

        if (socialAccount is null)
        {
            return ContentGuard.NotFound<ConversationDetailDto>("Social account");
        }

        // IDOR: verify workspace ownership.
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            socialAccount.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<ConversationDetailDto>.Fail(access.Error!, access.Code!.Value);
        }

        // Check if a conversation with this external ID already exists.
        var existing = await _inboxConversationRepository.GetByExternalIdAsync(
            request.SocialAccountId,
            request.ExternalConversationId,
            cancellationToken);

        if (existing is not null)
        {
            // Update participant information if changed.
            existing.UpdateParticipant(request.ExternalUserName, externalUserAvatarUrl: null);
            _inboxConversationRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reload with full navigations for mapping.
            var reloaded = await _inboxConversationRepository.GetByIdWithMessagesAsync(
                existing.Id, cancellationToken);

            var updatedDto = _mapper.Map<ConversationDetailDto>(reloaded);
            return Result<ConversationDetailDto>.Ok(updatedDto);
        }

        // Parse conversation type — default to DirectMessage if parsing fails.
        if (!Enum.TryParse<ConversationType>(request.Type, ignoreCase: true, out var conversationType))
        {
            conversationType = ConversationType.DirectMessage;
        }

        var conversation = InboxConversation.Create(
            socialAccount.WorkspaceId,
            socialAccount.Id,
            conversationType,
            request.ExternalConversationId,
            request.ExternalUserId,
            request.ExternalUserName,
            externalUserAvatarUrl: null,
            DateTime.UtcNow);

        await _inboxConversationRepository.AddAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Set navigation for mapping profile.
        typeof(InboxConversation).GetProperty(nameof(InboxConversation.SocialAccount))?
            .SetValue(conversation, socialAccount);

        var dto = _mapper.Map<ConversationDetailDto>(conversation);
        return Result<ConversationDetailDto>.Ok(dto);
    }
}

/// <summary>
/// Handles changing the workflow status of an inbox conversation (Open, Resolved, Snoozed).
/// Enforces workspace membership and IDOR checks.
/// </summary>
public sealed class ChangeConversationStatusCommandHandler
    : IRequestHandler<ChangeConversationStatusCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeConversationStatusCommandHandler"/> class.
    /// </summary>
    public ChangeConversationStatusCommandHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        ChangeConversationStatusCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound("Conversation");
        }

        // IDOR: verify workspace ownership.
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        conversation.ChangeStatus(request.Status);
        _inboxConversationRepository.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Handles assigning a conversation to a workspace member or clearing the assignment.
/// Replaces any existing assignment (one-to-one constraint).
/// </summary>
public sealed class AssignConversationCommandHandler
    : IRequestHandler<AssignConversationCommand, Result<ConversationAssignmentDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IConversationAssignmentRepository _conversationAssignmentRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignConversationCommandHandler"/> class.
    /// </summary>
    public AssignConversationCommandHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IConversationAssignmentRepository conversationAssignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _conversationAssignmentRepository = conversationAssignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<ConversationAssignmentDto>> Handle(
        AssignConversationCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound<ConversationAssignmentDto>("Conversation");
        }

        // IDOR: verify workspace ownership.
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<ConversationAssignmentDto>.Fail(access.Error!, access.Code!.Value);
        }

        // Remove existing assignment if present.
        var existingAssignment = await _conversationAssignmentRepository.GetByConversationIdAsync(
            request.ConversationId, cancellationToken);

        if (existingAssignment is not null)
        {
            _conversationAssignmentRepository.Remove(existingAssignment);
        }

        // If un-assigning (AssigneeUserId is null), just remove and return.
        if (request.Request.AssigneeUserId is null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ConversationAssignmentDto>.Ok(new ConversationAssignmentDto(
                request.ConversationId,
                AssignedToName: "Unassigned",
                AssignedByName: null,
                AssignedAt: DateTime.UtcNow,
                Note: request.Request.Note));
        }

        // Validate that the assignee is an active workspace member.
        var assigneeMember = await _workspaceMemberRepository.GetByUserAndWorkspaceAsync(
            request.Request.AssigneeUserId.Value,
            conversation.WorkspaceId,
            cancellationToken);

        if (assigneeMember is null || assigneeMember.Status != MemberStatus.Active)
        {
            return Result<ConversationAssignmentDto>.Fail(
                "The target user is not an active member of this workspace.",
                ErrorCode.Validation);
        }

        var assignment = ConversationAssignment.Create(
            request.ConversationId,
            request.Request.AssigneeUserId.Value,
            _currentUserContext.UserId,
            DateTime.UtcNow,
            request.Request.Note);

        await _conversationAssignmentRepository.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ConversationAssignmentDto>(assignment);
        return Result<ConversationAssignmentDto>.Ok(dto);
    }
}

/// <summary>
/// Handles marking all messages in a conversation as read by the team.
/// Uses bulk update for efficiency and resets the conversation unread counter.
/// </summary>
public sealed class MarkConversationReadCommandHandler
    : IRequestHandler<MarkConversationReadCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkConversationReadCommandHandler"/> class.
    /// </summary>
    public MarkConversationReadCommandHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        MarkConversationReadCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound("Conversation");
        }

        // IDOR: verify workspace membership.
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        // Bulk-mark all unread messages.
        await _inboxMessageRepository.MarkAsReadAsync(request.ConversationId, cancellationToken);

        // Reset the conversation-level unread counter.
        conversation.ClearUnread();
        _inboxConversationRepository.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Handles sending an outbound reply into a conversation via the platform messaging service.
/// Persists the outbound message and updates the conversation preview.
/// </summary>
public sealed class SendMessageCommandHandler
    : IRequestHandler<SendMessageCommand, Result<MessageDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IPlatformMessagingService _platformMessagingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendMessageCommandHandler"/> class.
    /// </summary>
    public SendMessageCommandHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ISocialAccountRepository socialAccountRepository,
        IPlatformMessagingService platformMessagingService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<SendMessageCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _socialAccountRepository = socialAccountRepository;
        _platformMessagingService = platformMessagingService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MessageDto>> Handle(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound<MessageDto>("Conversation");
        }

        // IDOR: verify workspace membership with write access.
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<MessageDto>.Fail(access.Error!, access.Code!.Value);
        }

        var socialAccount = await _socialAccountRepository.GetByIdAsync(
            conversation.SocialAccountId, cancellationToken);

        if (socialAccount is null)
        {
            return ContentGuard.NotFound<MessageDto>("Social account");
        }

        // Dispatch the message via the platform messaging service.
        var sendResult = await _platformMessagingService.SendConversationReplyAsync(
            socialAccount,
            conversation,
            request.Request.TextContent,
            request.Request.ReplyToMessageId,
            cancellationToken);

        if (!sendResult.IsSuccess)
        {
            _logger.LogWarning(
                "Platform rejected outbound message for conversation {ConversationId}: {Error}",
                conversation.Id,
                sendResult.ErrorMessage);

            return Result<MessageDto>.Fail(
                sendResult.ErrorMessage ?? "Failed to send message via platform API.",
                ErrorCode.ExternalApi);
        }

        var utcNow = sendResult.SentAtUtc ?? DateTime.UtcNow;

        var outboundMessage = InboxMessage.CreateOutbound(
            conversation.Id,
            sendResult.ExternalMessageId ?? Guid.NewGuid().ToString(),
            _currentUserContext.UserId,
            isAutomated: false,
            automationRuleId: null,
            MessageContentType.Text,
            request.Request.TextContent,
            mediaUrl: null,
            utcNow);

        await _inboxMessageRepository.AddAsync(outboundMessage, cancellationToken);

        // Update conversation last-message metadata.
        conversation.RegisterOutboundMessage(request.Request.TextContent, utcNow);
        _inboxConversationRepository.Update(conversation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<MessageDto>(outboundMessage);
        return Result<MessageDto>.Ok(dto);
    }
}

/// <summary>
/// Handles updating delivery status for an outbound message after the platform responds asynchronously.
/// Typically invoked by webhook callback processing.
/// </summary>
public sealed class UpdateMessageDeliveryStatusCommandHandler
    : IRequestHandler<UpdateMessageDeliveryStatusCommand, Result>
{
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMessageDeliveryStatusCommandHandler"/> class.
    /// </summary>
    public UpdateMessageDeliveryStatusCommandHandler(
        IInboxMessageRepository inboxMessageRepository,
        IUnitOfWork unitOfWork)
    {
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        UpdateMessageDeliveryStatusCommand request,
        CancellationToken cancellationToken)
    {
        var message = await _inboxMessageRepository.GetByIdAsync(
            request.MessageId, cancellationToken);

        if (message is null)
        {
            return ContentGuard.NotFound("Message");
        }

        // Only outbound messages have a delivery status.
        if (message.Direction != MessageDirection.Outbound)
        {
            return Result.Fail(
                "Delivery status can only be updated for outbound messages.",
                ErrorCode.Validation);
        }

        message.SetDeliveryStatus(request.DeliveryStatus);
        _inboxMessageRepository.Update(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Handles deleting a locally stored message when business rules allow removal.
/// Only outbound messages that have not yet been delivered can be deleted.
/// </summary>
public sealed class DeleteMessageCommandHandler
    : IRequestHandler<DeleteMessageCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteMessageCommandHandler"/> class.
    /// </summary>
    public DeleteMessageCommandHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        DeleteMessageCommand request,
        CancellationToken cancellationToken)
    {
        var message = await _inboxMessageRepository.GetByIdAsync(
            request.MessageId, cancellationToken);

        if (message is null)
        {
            return ContentGuard.NotFound("Message");
        }

        // Retrieve parent conversation for IDOR check.
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            message.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound("Conversation");
        }

        // IDOR: verify workspace ownership.
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        // Business rule: only outbound messages can be deleted locally.
        if (message.Direction != MessageDirection.Outbound)
        {
            return Result.Fail(
                "Only outbound messages can be deleted.",
                ErrorCode.Conflict);
        }

        _inboxMessageRepository.Remove(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  INBOX QUERY HANDLERS                                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Retrieves a paginated list of conversations for the workspace inbox with optional filtering.
/// </summary>
public sealed class GetConversationsPagedQueryHandler
    : IRequestHandler<GetConversationsPagedQuery, Result<PagedResult<ConversationSummaryDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetConversationsPagedQueryHandler"/> class.
    /// </summary>
    public GetConversationsPagedQueryHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ConversationSummaryDto>>> Handle(
        GetConversationsPagedQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<ConversationSummaryDto>>.Fail(access.Error!, access.Code!.Value);
        }

        // Parse optional filter values.
        Platform? platform = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.Platform) &&
            Enum.TryParse<Platform>(request.Filter.Platform, ignoreCase: true, out var parsedPlatform))
        {
            platform = parsedPlatform;
        }

        ConversationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.Status) &&
            Enum.TryParse<ConversationStatus>(request.Filter.Status, ignoreCase: true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var unreadOnly = request.Filter.UnreadOnly ?? false;
        var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;

        var items = await _inboxConversationRepository.GetPagedByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            platform,
            status,
            request.Filter.AssigneeId,
            unreadOnly,
            skip,
            request.Pagination.PageSize,
            cancellationToken);

        var totalCount = await _inboxConversationRepository.CountByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            platform,
            status,
            request.Filter.AssigneeId,
            unreadOnly,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<ConversationSummaryDto>>(items);
        var result = new PagedResult<ConversationSummaryDto>(
            dtos, totalCount, request.Pagination.Page, request.Pagination.PageSize);

        return Result<PagedResult<ConversationSummaryDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves one conversation with full message history and assignment context.
/// </summary>
public sealed class GetConversationDetailQueryHandler
    : IRequestHandler<GetConversationDetailQuery, Result<ConversationDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetConversationDetailQueryHandler"/> class.
    /// </summary>
    public GetConversationDetailQueryHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<ConversationDetailDto>> Handle(
        GetConversationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdWithMessagesAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound<ConversationDetailDto>("Conversation");
        }

        // IDOR: verify workspace membership.
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<ConversationDetailDto>.Fail(access.Error!, access.Code!.Value);
        }

        var dto = _mapper.Map<ConversationDetailDto>(conversation);
        return Result<ConversationDetailDto>.Ok(dto);
    }
}

/// <summary>
/// Retrieves the current unread conversation count for the workspace.
/// </summary>
public sealed class GetUnreadCountQueryHandler
    : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUnreadCountQueryHandler"/> class.
    /// </summary>
    public GetUnreadCountQueryHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
    }

    /// <inheritdoc />
    public async Task<Result<int>> Handle(
        GetUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<int>.Fail(access.Error!, access.Code!.Value);
        }

        var count = await _inboxConversationRepository.CountUnreadByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId, cancellationToken);

        return Result<int>.Ok(count);
    }
}

/// <summary>
/// Searches inbox conversations using the supplied filter and search term.
/// Returns search results with highlight snippets.
/// </summary>
public sealed class SearchConversationsQueryHandler
    : IRequestHandler<SearchConversationsQuery, Result<PagedResult<ConversationSearchResultDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchConversationsQueryHandler"/> class.
    /// </summary>
    public SearchConversationsQueryHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ConversationSearchResultDto>>> Handle(
        SearchConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<ConversationSearchResultDto>>.Fail(access.Error!, access.Code!.Value);
        }

        // Parse optional filter values.
        Platform? platform = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.Platform) &&
            Enum.TryParse<Platform>(request.Filter.Platform, ignoreCase: true, out var parsedPlatform))
        {
            platform = parsedPlatform;
        }

        ConversationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Filter.Status) &&
            Enum.TryParse<ConversationStatus>(request.Filter.Status, ignoreCase: true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var unreadOnly = request.Filter.UnreadOnly ?? false;
        var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;
        var searchTerm = request.Filter.Search ?? string.Empty;

        var items = await _inboxConversationRepository.SearchByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            searchTerm,
            platform,
            status,
            request.Filter.AssigneeId,
            unreadOnly,
            skip,
            request.Pagination.PageSize,
            cancellationToken);

        var totalCount = await _inboxConversationRepository.CountSearchByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            searchTerm,
            platform,
            status,
            request.Filter.AssigneeId,
            unreadOnly,
            cancellationToken);

        var dtos = items.Select(c =>
        {
            var dto = _mapper.Map<ConversationSearchResultDto>(c);
            // Provide a basic highlight using the search term.
            var highlight = c.LastMessagePreview?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true
                ? searchTerm
                : c.ExternalUserName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true
                    ? searchTerm
                    : null;
            return dto with { Highlight = highlight };
        }).ToList();

        var result = new PagedResult<ConversationSearchResultDto>(
            dtos, totalCount, request.Pagination.Page, request.Pagination.PageSize);

        return Result<PagedResult<ConversationSearchResultDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves paginated messages for one conversation.
/// </summary>
public sealed class GetMessagesPagedQueryHandler
    : IRequestHandler<GetMessagesPagedQuery, Result<PagedResult<MessageDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMessagesPagedQueryHandler"/> class.
    /// </summary>
    public GetMessagesPagedQueryHandler(
        ICurrentUserContext currentUserContext,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<MessageDto>>> Handle(
        GetMessagesPagedQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await _inboxConversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return ContentGuard.NotFound<PagedResult<MessageDto>>("Conversation");
        }

        // IDOR: verify workspace membership.
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            conversation.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<MessageDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;

        var messages = await _inboxMessageRepository.GetByConversationIdAsync(
            request.ConversationId, skip, request.Pagination.PageSize, cancellationToken);

        var totalCount = await _inboxMessageRepository.CountByConversationIdAsync(
            request.ConversationId, cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<MessageDto>>(messages);
        var result = new PagedResult<MessageDto>(
            dtos, totalCount, request.Pagination.Page, request.Pagination.PageSize);

        return Result<PagedResult<MessageDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves team workload information for inbox assignment balancing.
/// </summary>
public sealed class GetTeamWorkloadQueryHandler
    : IRequestHandler<GetTeamWorkloadQuery, Result<IReadOnlyList<TeamWorkloadDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IConversationAssignmentRepository _conversationAssignmentRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTeamWorkloadQueryHandler"/> class.
    /// </summary>
    public GetTeamWorkloadQueryHandler(
        ICurrentUserContext currentUserContext,
        IConversationAssignmentRepository conversationAssignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository)
    {
        _currentUserContext = currentUserContext;
        _conversationAssignmentRepository = conversationAssignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TeamWorkloadDto>>> Handle(
        GetTeamWorkloadQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<IReadOnlyList<TeamWorkloadDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var workload = await _conversationAssignmentRepository.GetWorkloadByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId, cancellationToken);

        return Result<IReadOnlyList<TeamWorkloadDto>>.Ok(workload);
    }
}
