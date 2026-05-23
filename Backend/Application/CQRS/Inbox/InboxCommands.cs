using Application.Common;
using Application.DTOs.Inbox;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Inbox;

/// <summary>
/// Creates a new conversation or updates an existing one from an external event source.
/// </summary>
/// <param name="SocialAccountId">Connected social account that owns the conversation.</param>
/// <param name="ExternalConversationId">Platform-side conversation identifier.</param>
/// <param name="Platform">Source platform as string.</param>
/// <param name="ExternalUserId">Platform-side identifier of the external participant.</param>
/// <param name="ExternalUserName">Display name of the external participant.</param>
/// <param name="Type">Conversation type such as direct message or comment thread.</param>
public sealed record CreateOrUpdateConversationCommand(
    Guid SocialAccountId,
    string ExternalConversationId,
    string Platform,
    string ExternalUserId,
    string ExternalUserName,
    string Type) : IRequest<Result<ConversationDetailDto>>;

/// <summary>
/// Changes the workflow status of an inbox conversation.
/// </summary>
/// <param name="ConversationId">Conversation whose status should change.</param>
/// <param name="Status">New workflow status.</param>
public sealed record ChangeConversationStatusCommand(Guid ConversationId, ConversationStatus Status) : IRequest<Result>;

/// <summary>
/// Assigns a conversation to a workspace member or removes its assignment.
/// </summary>
/// <param name="ConversationId">Conversation that should be assigned.</param>
/// <param name="Request">Assignment payload describing the assignee and optional note.</param>
public sealed record AssignConversationCommand(Guid ConversationId, AssignConversationRequest Request) : IRequest<Result<ConversationAssignmentDto>>;

/// <summary>
/// Marks all messages in a conversation as read by the team.
/// </summary>
/// <param name="ConversationId">Conversation whose unread state should be cleared.</param>
public sealed record MarkConversationReadCommand(Guid ConversationId) : IRequest<Result>;

/// <summary>
/// Sends an outbound reply into a conversation.
/// </summary>
/// <param name="ConversationId">Conversation that should receive the reply.</param>
/// <param name="Request">Outbound message payload.</param>
public sealed record SendMessageCommand(Guid ConversationId, SendMessageRequest Request) : IRequest<Result<MessageDto>>;

/// <summary>
/// Updates delivery status for an outbound message after the platform responds asynchronously.
/// </summary>
/// <param name="MessageId">Message whose delivery state should change.</param>
/// <param name="DeliveryStatus">New delivery status reported by the platform.</param>
public sealed record UpdateMessageDeliveryStatusCommand(Guid MessageId, MessageDeliveryStatus DeliveryStatus) : IRequest<Result>;

/// <summary>
/// Deletes a locally stored message when business rules allow removal.
/// </summary>
/// <param name="MessageId">Message that should be deleted.</param>
public sealed record DeleteMessageCommand(Guid MessageId) : IRequest<Result>;
