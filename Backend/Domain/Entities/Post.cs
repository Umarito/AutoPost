using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Центральная сущность публикационного домена.
/// Пост выражает намерение команды опубликовать определенный контент на один или несколько подключенных аккаунтов.
/// </summary>
public class Post : AuditableEntity<Guid>
{
    /// <summary>
    /// Creates a new post aggregate root.
    /// </summary>
    /// <param name="workspaceId">Workspace that owns the post.</param>
    /// <param name="createdByUserId">User who authored the post.</param>
    /// <param name="videoId">Optional attached video identifier.</param>
    /// <param name="content">Publication content value object.</param>
    /// <param name="schedule">Schedule value object.</param>
    /// <param name="createdAtUtc">UTC creation timestamp.</param>
    /// <returns>A fully initialized <see cref="Post"/> aggregate root.</returns>
    public static Post Create(
        Guid workspaceId,
        Guid createdByUserId,
        Guid? videoId,
        PostContent content,
        Schedule schedule,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(schedule);

        return new Post
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            CreatedByUserId = createdByUserId,
            VideoId = videoId,
            Content = content,
            Schedule = schedule,
            Status = PostStatus.Scheduled,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
    }

    /// <summary>
    /// Идентификатор рабочего пространства, в рамках которого создан пост.
    /// Поле задает tenant-контекст публикации и определяет область видимости данных.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Идентификатор пользователя, создавшего публикацию.
    /// Значение помогает строить аудит действий команды и фильтры вроде "мои посты".
    /// </summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Идентификатор видеоматериала, используемого в публикации.
    /// Значение может отсутствовать, если доменная модель будет расширена на другие типы контента, помимо видео.
    /// </summary>
    public Guid? VideoId { get; private set; }

    /// <summary>
    /// Текстовое и визуальное описание поста.
    /// Value object объединяет все поля контента, не требующие собственной идентичности.
    /// </summary>
    public PostContent Content { get; private set; } = default!;

    /// <summary>
    /// Параметры времени публикации и связи с планировщиком.
    /// Объект хранит момент отправки в UTC и сведения, необходимые для работы с отложенным запуском.
    /// </summary>
    public Schedule Schedule { get; private set; } = default!;

    /// <summary>
    /// Текущий агрегированный статус жизненного цикла поста.
    /// Поле показывает общее состояние публикации с учетом ее целевых площадок.
    /// </summary>
    public PostStatus Status { get; private set; }

    /// <summary>
    /// Момент фактического завершения публикации в UTC.
    /// Значение фиксирует финальное завершение процесса после публикации или окончательного провала.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Рабочее пространство, которому принадлежит пост.
    /// Навигация объединяет публикацию с ее tenant-контекстом и соседними объектами команды.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Пользователь, создавший публикацию.
    /// Навигация позволяет получить сведения об авторе внутри командных сценариев работы.
    /// </summary>
    public ApplicationUser CreatedBy { get; private set; } = default!;

    /// <summary>
    /// Видео, прикрепленное к публикации.
    /// Навигация связывает пост с медиафайлом, который затем распространяется по целевым платформам.
    /// </summary>
    public Video? Video { get; private set; }

    /// <summary>
    /// Набор целевых аккаунтов, на которые должен быть опубликован данный пост.
    /// Коллекция принадлежит агрегату Post и отражает мультиплатформенную природу продукта.
    /// </summary>
    public IReadOnlyCollection<PostTarget> Targets { get; private set; } = new List<PostTarget>();

    /// <summary>
    /// Replaces the content value object of the post.
    /// </summary>
    /// <param name="content">Updated content value object.</param>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void UpdateContent(PostContent content, DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Changes the attached video reference.
    /// </summary>
    /// <param name="videoId">New optional video identifier.</param>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void ChangeVideo(Guid? videoId, DateTime updatedAtUtc)
    {
        VideoId = videoId;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Changes the publication schedule and persists the scheduler correlation identifier.
    /// </summary>
    /// <param name="schedule">Updated schedule value object.</param>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void Reschedule(Schedule schedule, DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
        Status = PostStatus.Scheduled;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Marks the post as currently publishing.
    /// </summary>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void MarkPublishing(DateTime updatedAtUtc)
    {
        Status = PostStatus.Publishing;
        UpdatedAt = updatedAtUtc;
    }

    /// <summary>
    /// Cancels the post before publication begins.
    /// </summary>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void Cancel(DateTime updatedAtUtc)
    {
        Status = PostStatus.Cancelled;
        UpdatedAt = updatedAtUtc;
        CompletedAt = updatedAtUtc;
    }

    /// <summary>
    /// Applies the aggregate publication status after target execution changes.
    /// </summary>
    /// <param name="status">New aggregate status derived from post targets.</param>
    /// <param name="completedAtUtc">Optional UTC completion timestamp.</param>
    /// <param name="updatedAtUtc">UTC mutation timestamp.</param>
    public void ApplyPublicationOutcome(PostStatus status, DateTime? completedAtUtc, DateTime updatedAtUtc)
    {
        Status = status;
        CompletedAt = completedAtUtc;
        UpdatedAt = updatedAtUtc;
    }
}
