using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Загруженный видеофайл, который может многократно использоваться в публикациях.
/// Сущность отделена от поста, чтобы один и тот же медиа-ресурс можно было применять в нескольких публикационных сценариях.
/// </summary>
public class Video : BaseEntity<Guid>
{
    /// <summary>
    /// Creates a new uploaded video aggregate in the uploading or processing lifecycle.
    /// </summary>
    /// <param name="workspaceId">Workspace that owns the media asset.</param>
    /// <param name="uploadedByUserId">User who uploaded the file.</param>
    /// <param name="storageKey">Canonical object-storage key.</param>
    /// <param name="cdnUrl">Optional CDN URL when already known.</param>
    /// <param name="originalFileName">Original client-side file name.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="fileSizeBytes">Size of the file in bytes.</param>
    /// <param name="uploadedAt">UTC upload timestamp.</param>
    /// <returns>A newly initialized <see cref="Video"/> aggregate.</returns>
    public static Video Create(
        Guid workspaceId,
        Guid uploadedByUserId,
        string storageKey,
        string? cdnUrl,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        DateTime uploadedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new Video
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UploadedByUserId = uploadedByUserId,
            StorageKey = storageKey,
            CdnUrl = cdnUrl,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Status = VideoStatus.Uploading,
            UploadedAt = uploadedAt
        };
    }

    /// <summary>
    /// Идентификатор рабочего пространства, которому принадлежит видеоматериал.
    /// Поле гарантирует tenant-изоляцию медиафайлов и связанных операций.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Идентификатор пользователя, загрузившего видео в систему.
    /// Значение важно для аудита и отображения авторства при совместной работе команды.
    /// </summary>
    public Guid UploadedByUserId { get; private set; }

    /// <summary>
    /// Внутренний ключ файла в объектном хранилище.
    /// Поле описывает место хранения исходного медиафайла без привязки к конкретному способу генерации публичной ссылки.
    /// </summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>
    /// CDN-адрес или публичная ссылка, используемая для предпросмотра и воспроизведения видео в интерфейсе.
    /// Значение может быть заполнено после завершения загрузки и подготовки файла.
    /// </summary>
    public string? CdnUrl { get; private set; }

    /// <summary>
    /// Исходное имя файла, полученное от пользователя при загрузке.
    /// Поле помогает распознавать медиа внутри библиотеки и упрощает ручной поиск контента в UI.
    /// </summary>
    public string OriginalFileName { get; private set; } = default!;

    /// <summary>
    /// MIME-тип загруженного файла.
    /// Значение нужно для валидации входящего контента и выбора подходящей стратегии обработки.
    /// </summary>
    public string ContentType { get; private set; } = default!;

    /// <summary>
    /// Размер файла в байтах.
    /// Показатель используется для проверки ограничений, оценки способа загрузки и диагностики проблем с медиа.
    /// </summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Ссылка на миниатюру или превью-кадр видео.
    /// Изображение используется в медиабиблиотеке и при выборе контента для публикации.
    /// </summary>
    public string? ThumbnailUrl { get; private set; }

    /// <summary>
    /// Текущий статус жизненного цикла видео.
    /// Значение показывает, можно ли уже использовать файл в публикациях или он еще требует обработки.
    /// </summary>
    public VideoStatus Status { get; private set; }

    /// <summary>
    /// Текстовое описание ошибки, возникшей во время анализа или подготовки видео.
    /// Поле помогает команде и системе понять, почему файл нельзя использовать.
    /// </summary>
    public string? ProcessingError { get; private set; }

    /// <summary>
    /// Дата и время загрузки видео в систему.
    /// Метка отражает начало жизненного цикла медиафайла в рамках рабочего пространства.
    /// </summary>
    public DateTime UploadedAt { get; private set; }

    /// <summary>
    /// Дата и время мягкого удаления файла.
    /// Пока значение отсутствует, медиа считается доступным; после заполнения файл должен быть скрыт из обычной работы.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Рабочее пространство, которому принадлежит видеоматериал.
    /// Навигация помогает отследить tenant-контекст файла и все связанные ограничения доступа.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Пользователь, загрузивший файл.
    /// Навигация связывает медиа с человеком, который добавил его в библиотеку контента.
    /// </summary>
    public ApplicationUser UploadedBy { get; private set; } = default!;

    /// <summary>
    /// Технические характеристики видео, извлеченные после завершения обработки.
    /// Эта информация нужна для проверки совместимости с платформами и отображения параметров файла в интерфейсе.
    /// </summary>
    public VideoMetadata? Metadata { get; private set; }

    /// <summary>
    /// Публикации, в которых используется данный видеоматериал.
    /// Коллекция отражает переиспользование одного медиафайла в нескольких постах.
    /// </summary>
    public IReadOnlyCollection<Post> Posts { get; private set; } = new List<Post>();

    /// <summary>
    /// Moves the video into the processing state after the upload is finalized.
    /// </summary>
    public void MarkProcessing()
    {
        Status = VideoStatus.Processing;
        ProcessingError = null;
    }

    /// <summary>
    /// Stores extracted metadata for the uploaded media file.
    /// </summary>
    /// <param name="metadata">Normalized technical metadata.</param>
    public void SetMetadata(VideoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }

    /// <summary>
    /// Updates the canonical CDN URL used for playback or previews.
    /// </summary>
    /// <param name="cdnUrl">Public or canonical blob URL.</param>
    public void SetCdnUrl(string cdnUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cdnUrl);
        CdnUrl = cdnUrl;
    }

    /// <summary>
    /// Updates the preview thumbnail URL.
    /// </summary>
    /// <param name="thumbnailUrl">URL of the generated thumbnail asset.</param>
    public void SetThumbnail(string thumbnailUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailUrl);
        ThumbnailUrl = thumbnailUrl;
    }

    /// <summary>
    /// Marks the video as ready for reuse in posts after processing completes successfully.
    /// </summary>
    public void MarkReady()
    {
        Status = VideoStatus.Ready;
        ProcessingError = null;
    }

    /// <summary>
    /// Marks the video as failed and stores the processing error message.
    /// </summary>
    /// <param name="error">Human-readable processing failure details.</param>
    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = VideoStatus.Failed;
        ProcessingError = error;
    }

    /// <summary>
    /// Soft-deletes the video so it no longer participates in normal workspace queries.
    /// </summary>
    /// <param name="deletedAtUtc">UTC timestamp of the deletion request.</param>
    public void SoftDelete(DateTime deletedAtUtc)
    {
        Status = VideoStatus.Deleted;
        DeletedAt = deletedAtUtc;
    }
}
