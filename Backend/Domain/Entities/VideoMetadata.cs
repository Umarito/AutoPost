namespace Domain.Entities;

/// <summary>
/// Технические характеристики видеофайла, извлеченные после завершения загрузки.
/// Объект описывает свойства медиа без собственной идентичности и используется как value object.
/// </summary>
public class VideoMetadata
{
    /// <summary>
    /// Creates a normalized video-metadata value object.
    /// </summary>
    /// <param name="durationSeconds">Duration in whole seconds.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="aspectRatio">Human-readable aspect ratio.</param>
    /// <param name="frameRate">Frame rate value.</param>
    /// <param name="videoCodec">Primary video codec.</param>
    /// <param name="audioCodec">Optional audio codec.</param>
    /// <param name="videoBitrate">Video bitrate in bits per second.</param>
    /// <param name="hasAudio">Whether an audio track exists.</param>
    /// <returns>A normalized <see cref="VideoMetadata"/> value object.</returns>
    public static VideoMetadata Create(
        int durationSeconds,
        int width,
        int height,
        string aspectRatio,
        double frameRate,
        string videoCodec,
        string? audioCodec,
        long videoBitrate,
        bool hasAudio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aspectRatio);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoCodec);

        return new VideoMetadata
        {
            DurationSeconds = durationSeconds,
            Width = width,
            Height = height,
            AspectRatio = aspectRatio,
            FrameRate = frameRate,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            VideoBitrate = videoBitrate,
            HasAudio = hasAudio
        };
    }

    /// <summary>
    /// Продолжительность видеоматериала в секундах.
    /// Значение важно для проверки ограничений внешних платформ и планирования публикации.
    /// </summary>
    public int DurationSeconds { get; private set; }

    /// <summary>
    /// Ширина видео в пикселях.
    /// Поле помогает определять формат контента и его пригодность для разных платформ.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Высота видео в пикселях.
    /// В сочетании с шириной позволяет системе анализировать ориентацию и тип кадра.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Соотношение сторон видео в удобочитаемом формате.
    /// Значение помогает UI и бизнес-логике быстро определить подходящий сценарий публикации.
    /// </summary>
    public string AspectRatio { get; private set; } = default!;

    /// <summary>
    /// Частота кадров видео.
    /// Параметр полезен для технической диагностики качества файла и оценки его совместимости с платформами.
    /// </summary>
    public double FrameRate { get; private set; }

    /// <summary>
    /// Название видеокодека, использованного в исходном файле.
    /// Система может опираться на это поле при выявлении потенциально проблемных медиаформатов.
    /// </summary>
    public string VideoCodec { get; private set; } = default!;

    /// <summary>
    /// Название аудиокодека, если в файле присутствует аудиодорожка.
    /// Поле помогает определить, корректно ли будет обработан и опубликован медиафайл.
    /// </summary>
    public string? AudioCodec { get; private set; }

    /// <summary>
    /// Битрейт видеопотока.
    /// Значение используется для технической оценки качества и потенциальной пригодности видео для публикации.
    /// </summary>
    public long VideoBitrate { get; private set; }

    /// <summary>
    /// Признак наличия аудиодорожки в загруженном файле.
    /// Это важно для платформ и сценариев, где отсутствие звука может влиять на совместимость или ожидания пользователя.
    /// </summary>
    public bool HasAudio { get; private set; }
}
