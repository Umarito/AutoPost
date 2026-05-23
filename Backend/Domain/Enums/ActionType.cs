namespace Domain.Enums;

/// <summary>
/// Тип действия, которое система выполняет после успешного срабатывания правила автоматизации.
/// Значение определяет, какой бизнес-эффект должен быть произведен на платформе или в inbox.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Отправка личного сообщения внешнему пользователю.
    /// </summary>
    SendDirectMessage,

    /// <summary>
    /// Постановка отметки "нравится" на комментарий или реакцию пользователя.
    /// </summary>
    LikeComment,

    /// <summary>
    /// Публикация открытого ответа на комментарий.
    /// </summary>
    ReplyToComment,

    /// <summary>
    /// Добавление внутреннего бизнес-тега к диалогу в unified inbox.
    /// </summary>
    AddConversationTag,

    /// <summary>
    /// Назначение диалога конкретному сотруднику команды.
    /// </summary>
    AssignToTeamMember
}
