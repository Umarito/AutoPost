namespace Domain.Enums;

/// <summary>
/// Категория события, по которой пользователь может настраивать уведомления.
/// Значение связывает конкретный бизнес-сценарий с каналами оповещения команды.
/// </summary>
public enum NotificationEventType
{
    /// <summary>
    /// Уведомление об успешной публикации поста на всех целевых платформах.
    /// </summary>
    PostPublished,

    /// <summary>
    /// Уведомление о неуспешной публикации поста.
    /// </summary>
    PostFailed,

    /// <summary>
    /// Уведомление о новом входящем сообщении или обращении в unified inbox.
    /// </summary>
    NewInboxMessage,

    /// <summary>
    /// Уведомление о срабатывании правила автоматизации.
    /// </summary>
    AutomationTriggered,

    /// <summary>
    /// Уведомление о разрыве связи с подключенным социальным аккаунтом.
    /// </summary>
    SocialAccountDisconnected,

    /// <summary>
    /// Уведомление о приглашении нового участника в рабочее пространство.
    /// </summary>
    TeamMemberInvited
}
