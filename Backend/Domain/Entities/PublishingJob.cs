using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Журнал отдельной фоновой попытки публикации на платформу.
/// Сущность позволяет сохранять техническую историю исполнения для диагностики, повторных запусков и поддержки.
/// </summary>
public class PublishingJob : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new publishing-job audit entry in progress.
    /// </summary>
    /// <param name="postTargetId">Target that owns the publishing attempt.</param>
    /// <param name="attemptNumber">Sequential attempt number.</param>
    /// <param name="schedulerJobId">Optional Hangfire correlation identifier.</param>
    /// <param name="startedAtUtc">UTC start timestamp.</param>
    /// <returns>A fully initialized <see cref="PublishingJob"/> entity.</returns>
    public static PublishingJob Start(
        Guid postTargetId,
        int attemptNumber,
        string? schedulerJobId,
        DateTime startedAtUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            PostTargetId = postTargetId,
            AttemptNumber = attemptNumber,
            SchedulerJobId = schedulerJobId,
            StartedAt = startedAtUtc,
            Outcome = JobOutcome.InProgress
        };

    /// <summary>
    /// Идентификатор цели публикации, для которой была создана текущая попытка.
    /// Поле связывает журнал выполнения с конкретным каналом размещения.
    /// </summary>
    public Guid PostTargetId { get; private set; }

    /// <summary>
    /// Идентификатор задачи во внешнем планировщике.
    /// Значение используется для корреляции бизнес-журнала с инфраструктурными логами фоновой обработки.
    /// </summary>
    public string? SchedulerJobId { get; private set; }

    /// <summary>
    /// Порядковый номер попытки публикации для текущей цели.
    /// Поле позволяет отличать первый запуск от повторных исполнений после ошибок или временных ограничений API.
    /// </summary>
    public int AttemptNumber { get; private set; }

    /// <summary>
    /// Момент старта конкретной попытки публикации.
    /// Значение помогает анализировать длительность операций и интервалы между retry.
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Момент завершения попытки публикации.
    /// До окончания работы значение может отсутствовать.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Итог текущего запуска фоновой задачи.
    /// Поле фиксирует, завершилась ли попытка успешно, с ошибкой или была переведена в повторный запуск.
    /// </summary>
    public JobOutcome Outcome { get; private set; }

    /// <summary>
    /// Расширенные сведения об ошибке, включая технические детали, пригодные для диагностики.
    /// Содержимое предназначено для внутренних сценариев поддержки и не рассчитано на прямую выдачу клиенту.
    /// </summary>
    public string? ErrorDetails { get; private set; }

    /// <summary>
    /// Сырой ответ API платформы по результатам запроса публикации.
    /// Значение помогает восстанавливать контекст общения с внешним сервисом при анализе инцидентов.
    /// </summary>
    public string? RawApiResponse { get; private set; }

    /// <summary>
    /// Цель публикации, к которой относится текущая попытка.
    /// Навигация объединяет технический журнал с бизнес-объектом размещения контента.
    /// </summary>
    public PostTarget PostTarget { get; private set; } = default!;

    /// <summary>
    /// Completes the publishing attempt with its final outcome and diagnostics.
    /// </summary>
    /// <param name="outcome">Final job outcome.</param>
    /// <param name="completedAtUtc">UTC completion timestamp.</param>
    /// <param name="errorDetails">Optional error details.</param>
    /// <param name="rawApiResponse">Optional raw provider response.</param>
    public void Complete(
        JobOutcome outcome,
        DateTime completedAtUtc,
        string? errorDetails,
        string? rawApiResponse)
    {
        Outcome = outcome;
        CompletedAt = completedAtUtc;
        ErrorDetails = errorDetails;
        RawApiResponse = rawApiResponse;
    }
}
