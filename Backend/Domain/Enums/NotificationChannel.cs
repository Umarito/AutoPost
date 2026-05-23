namespace Domain.Enums;

/// <summary>
/// Определяет канал доставки уведомления, используемый operational-историей и диспетчеризацией.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Уведомление доставляется внутри приложения через real-time канал и центр уведомлений.
    /// </summary>
    InApp,

    /// <summary>
    /// Уведомление доставляется по электронной почте через асинхронный SMTP-пайплайн.
    /// </summary>
    Email,

    /// <summary>
    /// Уведомление доставляется через push-канал браузера или мобильного клиента.
    /// </summary>
    Push
}
