using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Снимок аналитики подключенного социального аккаунта на конкретный момент времени.
/// Сущность формирует временной ряд для оценки роста профиля и его общей динамики.
/// </summary>
public class SocialAccountInsight : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new social-account insight snapshot.
    /// </summary>
    /// <param name="socialAccountId">Connected account that owns the snapshot.</param>
    /// <param name="recordedAt">UTC timestamp when the metrics were captured.</param>
    /// <param name="followersCount">Follower or subscriber count.</param>
    /// <param name="followingCount">Following count when available.</param>
    /// <param name="totalPostsCount">Total number of published items on the account.</param>
    /// <param name="profileReach">Optional reach metric.</param>
    /// <param name="profileImpressions">Optional impressions metric.</param>
    /// <returns>A fully initialized <see cref="SocialAccountInsight"/> instance.</returns>
    public static SocialAccountInsight Create(
        Guid socialAccountId,
        DateTime recordedAt,
        long followersCount,
        long followingCount,
        long totalPostsCount,
        long? profileReach,
        long? profileImpressions)
    {
        return new SocialAccountInsight
        {
            Id = Guid.NewGuid(),
            SocialAccountId = socialAccountId,
            RecordedAt = recordedAt,
            FollowersCount = followersCount,
            FollowingCount = followingCount,
            TotalPostsCount = totalPostsCount,
            ProfileReach = profileReach,
            ProfileImpressions = profileImpressions
        };
    }

    /// <summary>
    /// Идентификатор социального аккаунта, для которого был собран аналитический снимок.
    /// Значение связывает временную метрику с конкретным внешним профилем.
    /// </summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Момент времени в UTC, к которому относится собранный набор метрик.
    /// Поле является временной координатой аналитического ряда.
    /// </summary>
    public DateTime RecordedAt { get; private set; }

    /// <summary>
    /// Количество подписчиков аккаунта на момент фиксации снимка.
    /// Показатель используется для анализа роста аудитории.
    /// </summary>
    public long FollowersCount { get; private set; }

    /// <summary>
    /// Количество аккаунтов, на которые подписан профиль, на момент фиксации снимка.
    /// Значение помогает анализировать стратегию поведения и соотношение входящей и исходящей аудитории.
    /// </summary>
    public long FollowingCount { get; private set; }

    /// <summary>
    /// Общее число опубликованных материалов в профиле на момент сбора аналитики.
    /// Показатель нужен для контекстного чтения динамики роста и активности аккаунта.
    /// </summary>
    public long TotalPostsCount { get; private set; }

    /// <summary>
    /// Суммарный охват профиля за отчетный период, если соответствующая платформа отдает такой показатель.
    /// Метрика помогает понимать, сколько уникальных пользователей увидели контент или сам профиль.
    /// </summary>
    public long? ProfileReach { get; private set; }

    /// <summary>
    /// Суммарное число показов профиля за период, если платформа поддерживает этот показатель.
    /// В отличие от охвата значение может включать повторные просмотры одними и теми же пользователями.
    /// </summary>
    public long? ProfileImpressions { get; private set; }

    /// <summary>
    /// Социальный аккаунт, к которому относится аналитический снимок.
    /// Навигация связывает временную метрику с основным интеграционным объектом домена.
    /// </summary>
    public SocialAccount SocialAccount { get; private set; } = default!;
}
