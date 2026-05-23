using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Описание содержимого публикации.
/// Объект группирует текстовые и визуальные параметры поста без собственной идентичности.
/// </summary>
public class PostContent
{
    /// <summary>
    /// Creates a new post-content value object.
    /// </summary>
    /// <param name="title">Optional title or short caption headline.</param>
    /// <param name="description">Optional body text or caption.</param>
    /// <param name="tags">Optional tag collection.</param>
    /// <param name="visibility">Target visibility mode.</param>
    /// <param name="customThumbnailUrl">Optional custom thumbnail URL.</param>
    /// <param name="platformSettingsJson">Optional provider-specific settings payload.</param>
    /// <returns>A fully initialized <see cref="PostContent"/> value object.</returns>
    public static PostContent Create(
        string? title,
        string? description,
        IReadOnlyList<string>? tags,
        Visibility visibility,
        string? customThumbnailUrl,
        string? platformSettingsJson)
    {
        return new PostContent
        {
            Title = title,
            Description = description,
            Tags = tags?.ToList() ?? [],
            Visibility = visibility,
            CustomThumbnailUrl = customThumbnailUrl,
            PlatformSettingsJson = platformSettingsJson
        };
    }

    /// <summary>
    /// Creates a new value object by applying a partial update over the current content.
    /// </summary>
    /// <param name="title">Optional replacement title.</param>
    /// <param name="description">Optional replacement description.</param>
    /// <param name="tags">Optional replacement tag collection.</param>
    /// <param name="visibility">Optional replacement visibility mode.</param>
    /// <param name="customThumbnailUrl">Optional replacement custom thumbnail URL.</param>
    /// <param name="platformSettingsJson">Optional replacement provider-specific settings.</param>
    /// <returns>The updated content value object.</returns>
    public PostContent Update(
        string? title,
        string? description,
        IReadOnlyList<string>? tags,
        Visibility? visibility,
        string? customThumbnailUrl = null,
        string? platformSettingsJson = null)
        => Create(
            title ?? Title,
            description ?? Description,
            tags ?? Tags,
            visibility ?? Visibility,
            customThumbnailUrl ?? CustomThumbnailUrl,
            platformSettingsJson ?? PlatformSettingsJson);

    /// <summary>
    /// Заголовок публикации, если соответствующая платформа требует или поддерживает его.
    /// Поле особенно важно для сценариев публикации на видеоплатформах вроде YouTube.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    /// Основной текст публикации или caption.
    /// Поле содержит смысловое описание поста и может участвовать в SEO, вовлечении и коммуникации с аудиторией.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Набор хэштегов, относящихся к публикации.
    /// Значения используются для платформенной оптимизации и поддерживают хранение набора меток как части контента поста.
    /// </summary>
    public IReadOnlyList<string> Tags { get; private set; } = new List<string>();

    /// <summary>
    /// Режим видимости, с которым контент должен быть опубликован на целевых платформах.
    /// Поле управляет тем, насколько публичным будет итоговый материал для внешней аудитории.
    /// </summary>
    public Visibility Visibility { get; private set; }

    /// <summary>
    /// Пользовательская ссылка на миниатюру публикации, если команда хочет переопределить автоматическое превью.
    /// Значение позволяет вручную управлять визуальным представлением контента.
    /// </summary>
    public string? CustomThumbnailUrl { get; private set; }

    /// <summary>
    /// JSON-строка с платформо-специфичными настройками публикации.
    /// Это поле служит расширяемым контейнером для параметров, которые нецелесообразно поднимать в отдельные доменные свойства на текущем этапе.
    /// </summary>
    public string? PlatformSettingsJson { get; private set; }
}
