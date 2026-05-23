using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Снимок аналитики опубликованного контента на конкретной платформе.
/// Сущность используется для хранения временных рядов метрик по опубликованным постам.
/// </summary>
public class PostAnalyticsSnapshot : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new post-analytics snapshot.
    /// </summary>
    /// <param name="postTargetId">Published target whose metrics were sampled.</param>
    /// <param name="recordedAt">UTC metric capture timestamp.</param>
    /// <param name="views">View count.</param>
    /// <param name="likes">Like count.</param>
    /// <param name="comments">Comment count.</param>
    /// <param name="shares">Share count.</param>
    /// <param name="saves">Save count.</param>
    /// <param name="reach">Optional unique reach metric.</param>
    /// <param name="impressions">Optional impressions metric.</param>
    /// <param name="averageWatchTime">Optional average watch time.</param>
    /// <param name="completionRate">Optional completion rate coefficient.</param>
    /// <returns>A fully initialized <see cref="PostAnalyticsSnapshot"/> entity.</returns>
    public static PostAnalyticsSnapshot Create(
        Guid postTargetId,
        DateTime recordedAt,
        long views,
        long likes,
        long comments,
        long shares,
        long saves,
        long? reach,
        long? impressions,
        double? averageWatchTime,
        double? completionRate)
        => new()
        {
            Id = Guid.NewGuid(),
            PostTargetId = postTargetId,
            RecordedAt = recordedAt,
            Views = views,
            Likes = likes,
            Comments = comments,
            Shares = shares,
            Saves = saves,
            Reach = reach,
            Impressions = impressions,
            AverageWatchTime = averageWatchTime,
            CompletionRate = completionRate
        };

    /// <summary>
    /// Идентификатор целевой публикации, для которой собраны метрики.
    /// Поле связывает аналитический снимок не с абстрактным постом, а с конкретным размещением на платформе.
    /// </summary>
    public Guid PostTargetId { get; private set; }

    /// <summary>
    /// Время сбора метрик в UTC.
    /// Значение задает точку временного ряда для последующего анализа динамики публикации.
    /// </summary>
    public DateTime RecordedAt { get; private set; }

    /// <summary>
    /// Количество просмотров публикации на момент снятия аналитики.
    /// Это одна из ключевых базовых метрик охвата контента.
    /// </summary>
    public long Views { get; private set; }

    /// <summary>
    /// Количество отметок "нравится" или аналогичных реакций.
    /// Метрика показывает базовый уровень позитивного вовлечения аудитории.
    /// </summary>
    public long Likes { get; private set; }

    /// <summary>
    /// Количество комментариев под публикацией.
    /// Значение помогает оценивать разговорную активность вокруг контента.
    /// </summary>
    public long Comments { get; private set; }

    /// <summary>
    /// Количество репостов, пересылок или аналогичных действий распространения.
    /// Метрика отражает вирусный потенциал материала.
    /// </summary>
    public long Shares { get; private set; }

    /// <summary>
    /// Количество сохранений или добавлений в закладки.
    /// Показатель важен для платформ, где сохранение контента сигнализирует о высокой практической ценности материала.
    /// </summary>
    public long Saves { get; private set; }

    /// <summary>
    /// Уникальный охват публикации, если платформа предоставляет такую метрику.
    /// В отличие от показов значение описывает число уникальных пользователей, увидевших материал.
    /// </summary>
    public long? Reach { get; private set; }

    /// <summary>
    /// Общее количество показов публикации.
    /// Метрика полезна для сравнения с охватом и анализа повторных просмотров.
    /// </summary>
    public long? Impressions { get; private set; }

    /// <summary>
    /// Среднее время просмотра контента в секундах.
    /// Показатель помогает понять, насколько долго аудитория удерживает внимание на материале.
    /// </summary>
    public double? AverageWatchTime { get; private set; }

    /// <summary>
    /// Доля пользователей, досмотревших видео до конца.
    /// Значение хранится как коэффициент и полезно для оценки качества контента и его соответствия ожиданиям аудитории.
    /// </summary>
    public double? CompletionRate { get; private set; }

    /// <summary>
    /// Целевая публикация, к которой относится аналитический снимок.
    /// Навигация связывает метрики с конкретным размещением на платформе.
    /// </summary>
    public PostTarget PostTarget { get; private set; } = default!;
}
