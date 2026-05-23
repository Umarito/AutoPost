namespace Domain.Enums;

/// <summary>
/// Поддерживаемые внешние платформы, с которыми взаимодействует AutoPost.
/// Значение определяет источник или цель публикации, коммуникации, аналитики и webhook-событий.
/// </summary>
public enum Platform
{
    /// <summary>
    /// Видеоплатформа YouTube.
    /// </summary>
    YouTube,

    /// <summary>
    /// Социальная сеть Instagram.
    /// </summary>
    Instagram,

    /// <summary>
    /// Социальная платформа Facebook.
    /// </summary>
    Facebook,

    /// <summary>
    /// Платформа коротких видео TikTok.
    /// </summary>
    TikTok,

    /// <summary>
    /// Социальная сеть Twitter или X в зависимости от актуального API-интеграционного контекста.
    /// </summary>
    Twitter,

    /// <summary>
    /// Мессенджер и контент-платформа Telegram.
    /// </summary>
    Telegram
}
