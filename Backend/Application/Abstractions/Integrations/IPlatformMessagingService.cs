using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Инкапсулирует исходящие messaging-операции платформ для inbox и automation сценариев.
/// Контракт нужен, чтобы CQRS-хендлеры не знали о конкретных HTTP endpoint'ах провайдеров.
/// </summary>
public interface IPlatformMessagingService
{
    /// <summary>
    /// Отправляет ответ в существующий диалог unified inbox.
    /// </summary>
    Task<PlatformMessageSendResult> SendConversationReplyAsync(
        SocialAccount socialAccount,
        InboxConversation conversation,
        string text,
        Guid? replyToMessageId,
        CancellationToken ct = default);

    /// <summary>
    /// Отправляет direct message внешнему пользователю.
    /// </summary>
    Task<PlatformMessageSendResult> SendDirectMessageAsync(
        SocialAccount socialAccount,
        string externalUserId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Публикует открытый ответ на внешний комментарий.
    /// </summary>
    Task<PlatformActionResult> ReplyToCommentAsync(
        SocialAccount socialAccount,
        string externalConversationId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Ставит лайк на внешний комментарий или аналогичный engagement-объект.
    /// </summary>
    Task<PlatformActionResult> LikeCommentAsync(
        SocialAccount socialAccount,
        string externalConversationId,
        CancellationToken ct = default);
}

/// <summary>
/// Нормализованный результат отправки сообщения через внешнюю платформу.
/// </summary>
public sealed record PlatformMessageSendResult(
    bool IsSuccess,
    string? ExternalMessageId,
    DateTime? SentAtUtc,
    string? ErrorMessage,
    string? RawResponse);

/// <summary>
/// Нормализованный результат выполнения не-message действия внешней платформы.
/// </summary>
public sealed record PlatformActionResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? RawResponse);
