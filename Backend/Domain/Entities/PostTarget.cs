using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Отдельная цель публикации поста на конкретный социальный аккаунт.
/// Сущность позволяет отслеживать результат размещения по каждой платформе независимо от общего статуса поста.
/// </summary>
public class PostTarget : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new post target in the pending state.
    /// </summary>
    /// <param name="postId">Owning post identifier.</param>
    /// <param name="socialAccountId">Connected social account identifier.</param>
    /// <param name="platform">Denormalized target platform.</param>
    /// <returns>A fully initialized <see cref="PostTarget"/> entity.</returns>
    public static PostTarget Create(Guid postId, Guid socialAccountId, Platform platform)
        => new()
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            SocialAccountId = socialAccountId,
            Platform = platform,
            Status = TargetStatus.Pending,
            Result = PostTargetResult.Create(null, null, null, null, null, 0)
        };

    /// <summary>
    /// Идентификатор поста, к которому относится данная цель публикации.
    /// Поле закрепляет принадлежность объекта агрегату Post.
    /// </summary>
    public Guid PostId { get; private set; }

    /// <summary>
    /// Идентификатор подключенного социального аккаунта, куда должен быть опубликован контент.
    /// Значение связывает цель с реальным внешним каналом размещения.
    /// </summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Денормализованное указание платформы для быстрого чтения и фильтрации без обязательной загрузки социального аккаунта.
    /// Поле делает выборки по типу платформы проще и дешевле на уровне чтения.
    /// </summary>
    public Platform Platform { get; private set; }

    /// <summary>
    /// Текущее состояние публикации на этом конкретном целевом аккаунте.
    /// Через поле можно понять, ждет ли цель запуска, уже публикуется или завершилась успехом либо ошибкой.
    /// </summary>
    public TargetStatus Status { get; private set; }

    /// <summary>
    /// Итог публикации на конкретной платформе.
    /// Value object хранит ссылку на внешний пост, данные об ошибке и счетчик попыток после завершения обработки.
    /// </summary>
    public PostTargetResult? Result { get; private set; }

    /// <summary>
    /// Пост, которому принадлежит текущая цель публикации.
    /// Навигация соединяет платформенный результат с основным агрегатом контентного плана.
    /// </summary>
    public Post Post { get; private set; } = default!;

    /// <summary>
    /// Социальный аккаунт, выбранный как место публикации.
    /// Навигация задает связь между публикационной целью и конкретной интеграцией.
    /// </summary>
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>
    /// История отдельных попыток публикации для данной цели.
    /// Коллекция нужна для аудита, диагностики повторных запусков и анализа нестабильных интеграций.
    /// </summary>
    public IReadOnlyCollection<PublishingJob> PublishingJobs { get; private set; } = new List<PublishingJob>();

    /// <summary>
    /// Marks the target as actively publishing.
    /// </summary>
    public void MarkPublishing()
    {
        Status = TargetStatus.Publishing;
    }

    /// <summary>
    /// Marks the target as queued for another attempt.
    /// </summary>
    public void MarkRetrying()
    {
        Status = TargetStatus.Retrying;
        Result = PostTargetResult.Create(
            Result?.ExternalPostId,
            Result?.ExternalPostUrl,
            Result?.PublishedAt,
            Result?.ErrorCode,
            Result?.ErrorMessage,
            (Result?.AttemptCount ?? 0) + 1);
    }

    /// <summary>
    /// Marks the target as successfully published on the remote platform.
    /// </summary>
    /// <param name="externalPostId">Platform-side post identifier.</param>
    /// <param name="externalPostUrl">Platform-side post URL when available.</param>
    /// <param name="publishedAtUtc">UTC timestamp when the platform accepted the publication.</param>
    public void MarkPublished(string? externalPostId, string? externalPostUrl, DateTime? publishedAtUtc)
    {
        Status = TargetStatus.Published;
        Result = PostTargetResult.Create(
            externalPostId,
            externalPostUrl,
            publishedAtUtc,
            null,
            null,
            Math.Max(1, Result?.AttemptCount ?? 1));
    }

    /// <summary>
    /// Marks the target as failed and stores provider diagnostics.
    /// </summary>
    /// <param name="errorCode">Optional machine-readable provider error code.</param>
    /// <param name="errorMessage">Human-readable failure details.</param>
    public void MarkFailed(string? errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Status = TargetStatus.Failed;
        Result = PostTargetResult.Create(
            Result?.ExternalPostId,
            Result?.ExternalPostUrl,
            Result?.PublishedAt,
            errorCode,
            errorMessage,
            Math.Max(1, Result?.AttemptCount ?? 1));
    }

    /// <summary>
    /// Increments the attempt counter without changing the published or failed outcome yet.
    /// </summary>
    public void IncrementAttempt()
    {
        Result = PostTargetResult.Create(
            Result?.ExternalPostId,
            Result?.ExternalPostUrl,
            Result?.PublishedAt,
            Result?.ErrorCode,
            Result?.ErrorMessage,
            (Result?.AttemptCount ?? 0) + 1);
    }
}
