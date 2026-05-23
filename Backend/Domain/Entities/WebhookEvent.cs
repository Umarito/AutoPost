using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Буфер входящего webhook-события от внешней платформы.
/// Сущность позволяет принять событие быстро, сохранить исходные данные и обработать их асинхронно без потери контекста.
/// </summary>
public class WebhookEvent : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новую buffered-запись входящего webhook-события.
    /// </summary>
    /// <param name="platform">Платформа-источник.</param>
    /// <param name="eventType">Тип события.</param>
    /// <param name="rawPayload">Исходный JSON payload.</param>
    /// <param name="signature">Подпись запроса.</param>
    /// <param name="isVerified">Результат проверки подписи.</param>
    /// <param name="receivedAtUtc">UTC-время приема.</param>
    /// <returns>Новая сущность <see cref="WebhookEvent"/>.</returns>
    public static WebhookEvent Create(
        Platform platform,
        string eventType,
        string rawPayload,
        string? signature,
        bool isVerified,
        DateTime receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayload);

        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Platform = platform,
            EventType = eventType.Trim(),
            RawPayload = rawPayload,
            Signature = string.IsNullOrWhiteSpace(signature) ? null : signature.Trim(),
            IsVerified = isVerified,
            ReceivedAt = receivedAtUtc,
            Status = isVerified ? WebhookEventStatus.Received : WebhookEventStatus.Ignored,
            ProcessingAttemptCount = 0
        };
    }

    /// <summary>
    /// Платформа, от которой получено событие.
    /// Поле определяет, какой парсер и какая бизнес-обработка должны быть применены к payload.
    /// </summary>
    public Platform Platform { get; private set; }

    /// <summary>
    /// Тип пришедшего события на языке платформы или интеграционного слоя.
    /// Значение помогает отличить комментарий, сообщение, подписку, упоминание и другие виды webhook-уведомлений.
    /// </summary>
    public string EventType { get; private set; } = default!;

    /// <summary>
    /// Исходный JSON payload, полученный от платформы.
    /// Поле хранится без потери данных, чтобы можно было повторно обработать событие при сбое или изменении логики.
    /// </summary>
    public string RawPayload { get; private set; } = default!;

    /// <summary>
    /// Подпись запроса, переданная платформой для проверки подлинности события.
    /// Значение используется для защиты webhook-пайплайна от подделанных уведомлений.
    /// </summary>
    public string? Signature { get; private set; }

    /// <summary>
    /// Признак успешной верификации подписи.
    /// Поле отделяет доверенные события от тех, которые были отклонены как потенциально поддельные.
    /// </summary>
    public bool IsVerified { get; private set; }

    /// <summary>
    /// Время фактического получения webhook-события в UTC.
    /// Это первая временная метка жизненного цикла события внутри системы.
    /// </summary>
    public DateTime ReceivedAt { get; private set; }

    /// <summary>
    /// Текущее состояние обработки события.
    /// Поле позволяет разделить этап быстрого приема уведомления и этап содержательной фоновой обработки.
    /// </summary>
    public WebhookEventStatus Status { get; private set; }

    /// <summary>
    /// Количество попыток обработки события.
    /// Значение полезно для стратегии повторных запусков и диагностики нестабильных входящих данных.
    /// </summary>
    public int ProcessingAttemptCount { get; private set; }

    /// <summary>
    /// Время завершения обработки события в UTC.
    /// Поле заполняется после успешного исполнения или окончательного завершения с ошибкой.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Текстовое описание ошибки обработки, если событие не удалось разобрать или применить.
    /// Значение помогает расследовать проблемы интеграционного пайплайна.
    /// </summary>
    public string? ProcessingError { get; private set; }

    /// <summary>
    /// Переводит событие в состояние фоновой обработки.
    /// </summary>
    public void MarkProcessing()
    {
        Status = WebhookEventStatus.Processing;
        ProcessingAttemptCount++;
        ProcessingError = null;
    }

    /// <summary>
    /// Помечает событие как успешно обработанное.
    /// </summary>
    /// <param name="processedAtUtc">UTC-время завершения обработки.</param>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        Status = WebhookEventStatus.Processed;
        ProcessedAt = processedAtUtc;
        ProcessingError = null;
    }

    /// <summary>
    /// Помечает событие как проигнорированное без дальнейшей обработки.
    /// </summary>
    /// <param name="processedAtUtc">UTC-время завершения решения.</param>
    /// <param name="reason">Причина игнорирования.</param>
    public void MarkIgnored(DateTime processedAtUtc, string? reason)
    {
        Status = WebhookEventStatus.Ignored;
        ProcessedAt = processedAtUtc;
        ProcessingError = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>
    /// Фиксирует неуспешную обработку события.
    /// </summary>
    /// <param name="processedAtUtc">UTC-время завершения попытки.</param>
    /// <param name="error">Описание ошибки.</param>
    public void MarkFailed(DateTime processedAtUtc, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Status = WebhookEventStatus.Failed;
        ProcessedAt = processedAtUtc;
        ProcessingError = error.Trim();
    }

    /// <summary>
    /// Возвращает событие в очередь повторной обработки.
    /// </summary>
    public void ResetForReprocess()
    {
        Status = WebhookEventStatus.Received;
        ProcessedAt = null;
        ProcessingError = null;
    }
}
