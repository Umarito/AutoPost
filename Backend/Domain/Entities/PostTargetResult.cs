namespace Domain.Entities;

/// <summary>
/// Итог публикации поста на конкретной платформе.
/// Объект не имеет собственной идентичности и используется как value object, вложенный в PostTarget.
/// </summary>
public class PostTargetResult
{
    /// <summary>
    /// Creates a new target-result value object.
    /// </summary>
    /// <param name="externalPostId">Optional remote post identifier.</param>
    /// <param name="externalPostUrl">Optional remote post URL.</param>
    /// <param name="publishedAt">Optional UTC publication timestamp.</param>
    /// <param name="errorCode">Optional provider error code.</param>
    /// <param name="errorMessage">Optional human-readable error message.</param>
    /// <param name="attemptCount">Number of attempts already consumed.</param>
    /// <returns>A fully initialized <see cref="PostTargetResult"/> value object.</returns>
    public static PostTargetResult Create(
        string? externalPostId,
        string? externalPostUrl,
        DateTime? publishedAt,
        string? errorCode,
        string? errorMessage,
        int attemptCount)
        => new()
        {
            ExternalPostId = externalPostId,
            ExternalPostUrl = externalPostUrl,
            PublishedAt = publishedAt,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            AttemptCount = attemptCount
        };

    /// <summary>
    /// Идентификатор опубликованного поста на стороне внешней платформы.
    /// Поле требуется для дальнейшего получения аналитики и построения глубоких ссылок.
    /// </summary>
    public string? ExternalPostId { get; private set; }

    /// <summary>
    /// Прямая ссылка на опубликованный материал на внешней платформе.
    /// Значение удобно для перехода из интерфейса AutoPost к результату публикации.
    /// </summary>
    public string? ExternalPostUrl { get; private set; }

    /// <summary>
    /// Время фактической публикации на стороне платформы в UTC.
    /// Поле помогает сравнить запланированный момент с реальным временем размещения.
    /// </summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>
    /// Машиночитаемый код ошибки, полученный от API платформы.
    /// Поле используется для маршрутизации повторных попыток и более точной диагностики сбоев.
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// Текстовое описание ошибки, вернувшееся из API платформы или из слоя публикации.
    /// Значение помогает человеку понять причину неудачи без изучения сырых логов.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Общее количество попыток публикации на данную цель.
    /// Счетчик важен для анализа устойчивости интеграции и истории повторных запусков.
    /// </summary>
    public int AttemptCount { get; private set; }
}
