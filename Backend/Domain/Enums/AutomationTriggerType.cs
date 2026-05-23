namespace Domain.Enums;

/// <summary>
/// Тип внешнего события, способного запустить правило автоматизации.
/// Значение определяет, на какие действия аудитории или платформы реагирует система.
/// </summary>
public enum AutomationTriggerType
{
    /// <summary>
    /// Новый комментарий под контентом подключенного аккаунта.
    /// </summary>
    NewComment,

    /// <summary>
    /// Появление нового подписчика у подключенного аккаунта.
    /// </summary>
    NewFollower,

    /// <summary>
    /// Упоминание аккаунта в story или аналогичном эфемерном контенте.
    /// </summary>
    StoryMention,

    /// <summary>
    /// Получение входящего личного сообщения.
    /// </summary>
    DirectMessageReceived,

    /// <summary>
    /// Получение комментария, важного с точки зрения ключевого слова или сценария реакции.
    /// </summary>
    CommentKeyword
}
