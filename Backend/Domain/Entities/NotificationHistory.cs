using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Хранит факт генерации и доставки одного уведомления по конкретному каналу.
/// Сущность нужна для notification center, аудита и мониторинга надежности каналов доставки.
/// </summary>
public class NotificationHistory : BaseEntity<Guid>
{
    /// <summary>
    /// Идентификатор пользователя, которому предназначалось уведомление.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Идентификатор рабочего пространства, в контексте которого было создано уведомление.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Бизнес-тип события, вызвавшего уведомление.
    /// </summary>
    public NotificationEventType EventType { get; private set; }

    /// <summary>
    /// Канал доставки, по которому было отправлено уведомление.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Заголовок уведомления, показываемый пользователю.
    /// </summary>
    public string Title { get; private set; } = default!;

    /// <summary>
    /// Основной текст уведомления.
    /// </summary>
    public string Body { get; private set; } = default!;

    /// <summary>
    /// Опциональная ссылка или маршрут, открываемый клиентом при взаимодействии с уведомлением.
    /// </summary>
    public string? ActionUrl { get; private set; }

    /// <summary>
    /// Время создания записи истории в UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Время подтвержденной доставки уведомления в UTC.
    /// Для queued-каналов фиксируется в момент успешной постановки в безопасную очередь доставки.
    /// </summary>
    public DateTime? DeliveredAt { get; private set; }

    /// <summary>
    /// Последнее описание ошибки доставки, если канал завершился неуспешно.
    /// </summary>
    public string? DeliveryError { get; private set; }

    /// <summary>
    /// Признак успешной доставки или безопасной постановки уведомления в надежный delivery pipeline.
    /// </summary>
    public bool IsDelivered { get; private set; }

    /// <summary>
    /// Создает новую запись operational-истории уведомления.
    /// </summary>
    /// <param name="userId">Идентификатор получателя.</param>
    /// <param name="workspaceId">Идентификатор рабочего пространства.</param>
    /// <param name="eventType">Тип уведомительного события.</param>
    /// <param name="channel">Канал доставки.</param>
    /// <param name="title">Заголовок уведомления.</param>
    /// <param name="body">Текст уведомления.</param>
    /// <param name="actionUrl">Опциональная ссылка действия.</param>
    /// <param name="createdAtUtc">UTC-время создания записи.</param>
    /// <returns>Новая сущность <see cref="NotificationHistory"/>.</returns>
    public static NotificationHistory Create(
        Guid userId,
        Guid workspaceId,
        NotificationEventType eventType,
        NotificationChannel channel,
        string title,
        string body,
        string? actionUrl,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new NotificationHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            EventType = eventType,
            Channel = channel,
            Title = title.Trim(),
            Body = body.Trim(),
            ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
            CreatedAt = createdAtUtc
        };
    }

    /// <summary>
    /// Помечает запись истории как успешно доставленную или безопасно принятую в фоновую очередь доставки.
    /// </summary>
    /// <param name="deliveredAtUtc">UTC-время успешной доставки.</param>
    public void MarkDelivered(DateTime deliveredAtUtc)
    {
        IsDelivered = true;
        DeliveredAt = deliveredAtUtc;
        DeliveryError = null;
    }

    /// <summary>
    /// Фиксирует ошибку канала доставки без удаления истории инцидента.
    /// </summary>
    /// <param name="error">Описание ошибки доставки.</param>
    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        IsDelivered = false;
        DeliveryError = error.Trim();
    }
}
