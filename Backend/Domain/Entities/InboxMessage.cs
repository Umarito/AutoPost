using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Отдельное сообщение внутри conversation.
/// Сущность хранит входящие и исходящие элементы переписки, включая автоматические ответы системы.
/// </summary>
public class InboxMessage : BaseEntity<Guid>
{
    /// <summary>
    /// Создает входящее сообщение, пришедшее от внешней платформы или пользователя.
    /// </summary>
    /// <param name="conversationId">Идентификатор диалога.</param>
    /// <param name="externalMessageId">Внешний идентификатор сообщения.</param>
    /// <param name="contentType">Тип содержимого.</param>
    /// <param name="textContent">Текст сообщения.</param>
    /// <param name="mediaUrl">Ссылка на медиа.</param>
    /// <param name="sentAtUtc">UTC-время отправки/получения.</param>
    /// <returns>Новая сущность входящего сообщения.</returns>
    public static InboxMessage CreateInbound(
        Guid conversationId,
        string externalMessageId,
        MessageContentType contentType,
        string? textContent,
        string? mediaUrl,
        DateTime sentAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalMessageId);

        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ExternalMessageId = externalMessageId.Trim(),
            Direction = MessageDirection.Inbound,
            ContentType = contentType,
            TextContent = string.IsNullOrWhiteSpace(textContent) ? null : textContent.Trim(),
            MediaUrl = string.IsNullOrWhiteSpace(mediaUrl) ? null : mediaUrl.Trim(),
            SentAt = sentAtUtc,
            IsReadByTeam = false
        };
    }

    /// <summary>
    /// Создает исходящее сообщение, отправленное вручную или автоматизацией.
    /// </summary>
    /// <param name="conversationId">Идентификатор диалога.</param>
    /// <param name="externalMessageId">Внешний идентификатор сообщения.</param>
    /// <param name="sentByUserId">Пользователь-отправитель, если сообщение ручное.</param>
    /// <param name="isAutomated">Признак автоматической отправки.</param>
    /// <param name="automationRuleId">Правило автоматизации-источник.</param>
    /// <param name="contentType">Тип содержимого.</param>
    /// <param name="textContent">Текст сообщения.</param>
    /// <param name="mediaUrl">Ссылка на медиа.</param>
    /// <param name="sentAtUtc">UTC-время отправки.</param>
    /// <returns>Новая сущность исходящего сообщения.</returns>
    public static InboxMessage CreateOutbound(
        Guid conversationId,
        string externalMessageId,
        Guid? sentByUserId,
        bool isAutomated,
        Guid? automationRuleId,
        MessageContentType contentType,
        string? textContent,
        string? mediaUrl,
        DateTime sentAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalMessageId);

        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ExternalMessageId = externalMessageId.Trim(),
            Direction = MessageDirection.Outbound,
            SentByUserId = sentByUserId,
            IsAutomated = isAutomated,
            AutomationRuleId = automationRuleId,
            ContentType = contentType,
            TextContent = string.IsNullOrWhiteSpace(textContent) ? null : textContent.Trim(),
            MediaUrl = string.IsNullOrWhiteSpace(mediaUrl) ? null : mediaUrl.Trim(),
            SentAt = sentAtUtc,
            IsReadByTeam = true,
            DeliveryStatus = MessageDeliveryStatus.Sending
        };
    }

    /// <summary>
    /// Идентификатор диалога, к которому относится сообщение.
    /// Поле объединяет отдельные элементы общения в единую conversation.
    /// </summary>
    public Guid ConversationId { get; private set; }

    /// <summary>
    /// Идентификатор сообщения на стороне внешней платформы.
    /// Значение необходимо для дедупликации webhook-уведомлений и выполнения ответов или сопоставлений.
    /// </summary>
    public string ExternalMessageId { get; private set; } = default!;

    /// <summary>
    /// Направление сообщения относительно команды.
    /// Поле показывает, пришло ли сообщение от внешнего пользователя или было отправлено из AutoPost наружу.
    /// </summary>
    public MessageDirection Direction { get; private set; }

    /// <summary>
    /// Идентификатор участника команды, отправившего сообщение вручную.
    /// Значение отсутствует для входящих сообщений и для полностью автоматических ответов.
    /// </summary>
    public Guid? SentByUserId { get; private set; }

    /// <summary>
    /// Признак того, что сообщение было отправлено автоматизацией, а не человеком.
    /// Поле помогает отличать ручную работу менеджера от сценариев DM Automation.
    /// </summary>
    public bool IsAutomated { get; private set; }

    /// <summary>
    /// Идентификатор правила автоматизации, породившего сообщение.
    /// Значение заполняется для сценариев, когда система сама сформировала исходящий ответ.
    /// </summary>
    public Guid? AutomationRuleId { get; private set; }

    /// <summary>
    /// Тип содержимого сообщения.
    /// Поле определяет, как система должна хранить, отображать и интерпретировать полезную нагрузку сообщения.
    /// </summary>
    public MessageContentType ContentType { get; private set; }

    /// <summary>
    /// Текстовое содержимое сообщения, если оно присутствует.
    /// Поле может использоваться как основной контент либо как подпись к вложению.
    /// </summary>
    public string? TextContent { get; private set; }

    /// <summary>
    /// Ссылка на медиафайл или вложение, если сообщение содержит не только текст.
    /// Значение помогает отрисовывать изображения, видео и другие медиа внутри inbox.
    /// </summary>
    public string? MediaUrl { get; private set; }

    /// <summary>
    /// Момент фактической отправки или получения сообщения в UTC.
    /// Поле является основной временной меткой для хронологии общения.
    /// </summary>
    public DateTime SentAt { get; private set; }

    /// <summary>
    /// Признак того, что сообщение уже было просмотрено кем-либо из команды.
    /// Значение используется для контроля очереди входящих и рабочих SLA.
    /// </summary>
    public bool IsReadByTeam { get; private set; }

    /// <summary>
    /// Время, когда сообщение было прочитано командой.
    /// Поле помогает анализировать скорость реакции на обращения пользователей.
    /// </summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>
    /// Статус доставки исходящего сообщения.
    /// Для входящих сообщений поле обычно остается пустым, потому что их доставка контролируется внешней платформой.
    /// </summary>
    public MessageDeliveryStatus? DeliveryStatus { get; private set; }

    /// <summary>
    /// Диалог, частью которого является сообщение.
    /// Навигация связывает конкретный элемент переписки с общей conversation.
    /// </summary>
    public InboxConversation Conversation { get; private set; } = default!;

    /// <summary>
    /// Пользователь команды, вручную отправивший исходящее сообщение.
    /// Навигация может отсутствовать для входящих и автоматических сообщений.
    /// </summary>
    public ApplicationUser? SentBy { get; private set; }

    /// <summary>
    /// Правило автоматизации, которое создало сообщение.
    /// Навигация нужна для трассировки автоматических реакций и последующего анализа их эффективности.
    /// </summary>
    public AutomationRule? AutomationRule { get; private set; }

    /// <summary>
    /// Помечает сообщение как прочитанное командой.
    /// </summary>
    /// <param name="readAtUtc">UTC-время прочтения.</param>
    public void MarkRead(DateTime readAtUtc)
    {
        IsReadByTeam = true;
        ReadAt = readAtUtc;
    }

    /// <summary>
    /// Обновляет статус доставки исходящего сообщения.
    /// </summary>
    /// <param name="deliveryStatus">Новый статус доставки.</param>
    public void SetDeliveryStatus(MessageDeliveryStatus deliveryStatus)
    {
        DeliveryStatus = deliveryStatus;
    }
}
