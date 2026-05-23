using System.ComponentModel.DataAnnotations;
using Application.DTOs.Workspace;

namespace Application.DTOs.Inbox;

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Filter parameters for listing inbox conversations.
/// TRD API: GET /api/inbox/conversations?platform=Instagram&amp;status=Open
/// </summary>
/// <param name="Platform">Filter by platform, or null for all.</param>
/// <param name="Status">Filter: "Open", "Resolved", "Snoozed", or null.</param>
/// <param name="AssigneeId">Filter by assigned team member, or null.</param>
/// <param name="UnreadOnly">If true, only conversations with unread messages.</param>
/// <param name="Search">Text search against user name or content, or null.</param>
public record InboxFilterRequest(
    string? Platform, string? Status, Guid? AssigneeId,
    bool? UnreadOnly, string? Search
);

/// <summary>
/// Payload for sending a reply. Creates an outbound message and dispatches it to the platform API.
/// TRD API: POST /api/inbox/conversations/{id}/messages
/// </summary>
public record SendMessageRequest(
    [Required, MaxLength(5000)] string TextContent,
    Guid? ReplyToMessageId = null);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Compact conversation for the inbox sidebar list.
/// Shows external user identity, last message preview, unread count, and assignment.
/// </summary>
public record ConversationSummaryDto(
    Guid Id, string Platform, string Type,
    string ExternalUserName, string? ExternalUserAvatarUrl,
    string? LastMessagePreview, DateTime? LastMessageAt,
    string Status, int UnreadCount, string? AssigneeName
);

/// <summary>
/// Результат поиска диалога с сохранением превью совпадения и краткого сниппета.
/// DTO нужен отдельным типом, потому что TRD требует подсветку найденного контекста,
/// а не только стандартную summary-модель списка диалогов.
/// </summary>
/// <param name="ConversationId">Идентификатор найденного диалога.</param>
/// <param name="Platform">Платформа диалога.</param>
/// <param name="Type">Тип диалога.</param>
/// <param name="ExternalUserName">Имя внешнего собеседника.</param>
/// <param name="Status">Текущий рабочий статус диалога.</param>
/// <param name="UnreadCount">Количество непрочитанных входящих сообщений.</param>
/// <param name="LastMessageAt">Время последнего сообщения.</param>
/// <param name="Highlight">Короткий фрагмент совпавшего текста для UI-подсветки.</param>
/// <param name="Snippet">Контекстный сниппет результата поиска.</param>
public record ConversationSearchResultDto(
    Guid ConversationId,
    string Platform,
    string Type,
    string ExternalUserName,
    string Status,
    int UnreadCount,
    DateTime? LastMessageAt,
    string? Highlight,
    string? Snippet
);

/// <summary>
/// Full conversation with message history for the chat thread view.
/// Adds: external user ID, follow status, assignee details, and full message list.
/// </summary>
public record ConversationDetailDto(
    Guid Id, string Platform, string Type,
    string ExternalUserId, string ExternalUserName, string? ExternalUserAvatarUrl,
    bool IsFollowingUs, string Status, int UnreadCount,
    MemberDto? Assignee, List<MessageDto> Messages
);

/// <summary>
/// Single message in a conversation thread.
/// Direction: "Inbound" (from external user) or "Outbound" (from team/automation).
/// IsAutomated = true means the message was sent by an AutomationRule action.
/// </summary>
public record MessageDto(
    Guid Id, string Direction, string ContentType,
    string? TextContent, string? MediaUrl, bool IsAutomated,
    string? SentByName, DateTime SentAt, bool IsReadByTeam,
    string? DeliveryStatus
);
