namespace Domain.Entities;

/// <summary>
/// Временные параметры публикации.
/// Value object описывает, когда пост должен быть запущен и с какой задачей планировщика он связан.
/// </summary>
public class Schedule
{
    /// <summary>
    /// Creates a new publication schedule value object.
    /// </summary>
    /// <param name="scheduledAt">UTC publication timestamp.</param>
    /// <param name="timeZoneId">User-facing IANA timezone identifier.</param>
    /// <param name="schedulerJobId">Optional scheduler correlation identifier.</param>
    /// <returns>A fully initialized <see cref="Schedule"/> value object.</returns>
    public static Schedule Create(DateTime scheduledAt, string timeZoneId, string? schedulerJobId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        return new Schedule
        {
            ScheduledAt = scheduledAt,
            TimeZoneId = timeZoneId,
            SchedulerJobId = schedulerJobId
        };
    }

    /// <summary>
    /// Creates a copy of the current schedule with updated publication timing.
    /// </summary>
    /// <param name="scheduledAt">New UTC publication timestamp.</param>
    /// <param name="timeZoneId">New user-facing IANA timezone identifier.</param>
    /// <returns>An updated <see cref="Schedule"/> value object.</returns>
    public Schedule Reschedule(DateTime scheduledAt, string timeZoneId)
        => Create(scheduledAt, timeZoneId, SchedulerJobId);

    /// <summary>
    /// Creates a copy of the current schedule with a new scheduler correlation identifier.
    /// </summary>
    /// <param name="schedulerJobId">Scheduler job identifier to persist.</param>
    /// <returns>An updated <see cref="Schedule"/> value object.</returns>
    public Schedule WithSchedulerJobId(string? schedulerJobId)
        => Create(ScheduledAt, TimeZoneId, schedulerJobId);

    /// <summary>
    /// Время плановой публикации в UTC.
    /// Согласно TRD это основное бизнес-время постинга, и конвертация в локальную зону должна происходить только на уровне отображения.
    /// </summary>
    public DateTime ScheduledAt { get; private set; }

    /// <summary>
    /// Часовой пояс пользователя в формате IANA, актуальный на момент планирования.
    /// Поле сохраняется для корректного отображения выбранного времени в интерфейсе без нарушения UTC-модели хранения.
    /// </summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>
    /// Идентификатор фоновой задачи планировщика, отвечающей за запуск публикации.
    /// Значение нужно для отмены, переноса или диагностики отложенного постинга.
    /// </summary>
    public string? SchedulerJobId { get; private set; }
}
