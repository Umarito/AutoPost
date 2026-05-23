using Domain.Enums;

namespace Application.Abstractions.Webhooks;

/// <summary>
/// Разбирает raw webhook payload в нормализованное представление, понятное Application-слою.
/// Контракт позволяет маршрутизировать входящие события без знания платформенного формата JSON.
/// </summary>
public interface IWebhookPayloadParser
{
    /// <summary>
    /// Платформа, payload которой поддерживает текущий парсер.
    /// </summary>
    Platform Platform { get; }

    /// <summary>
    /// Пытается распарсить webhook payload в нормализованное событие.
    /// </summary>
    Task<WebhookPayloadParseResult> ParseAsync(string eventType, string rawPayload, CancellationToken ct = default);
}

/// <summary>
/// Фабрика, выбирающая платформенный webhook parser.
/// </summary>
public interface IWebhookPayloadParserFactory
{
    /// <summary>
    /// Возвращает парсер для указанной платформы.
    /// </summary>
    IWebhookPayloadParser GetParser(Platform platform);
}

/// <summary>
/// Результат нормализации webhook payload.
/// </summary>
public sealed record WebhookPayloadParseResult(
    bool IsSuccess,
    string? Error,
    NormalizedWebhookEvent? Event);

/// <summary>
/// Нормализованное представление входящего webhook-события, используемое в CQRS-хендлерах.
/// </summary>
public sealed record NormalizedWebhookEvent(
    Platform Platform,
    string EventType,
    string? ExternalEventId,
    Guid? SocialAccountId,
    string? ExternalConversationId,
    string? ExternalMessageId,
    string? ExternalUserId,
    string? ExternalUserName,
    string? ExternalUserAvatarUrl,
    string? ExternalPostId,
    string? ContentText,
    bool? IsFollowingUs,
    DateTime OccurredAtUtc,
    string RawPayload);
