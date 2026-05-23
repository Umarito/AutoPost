namespace Domain.Enums;

/// <summary>
/// Вид коммуникации, агрегируемой в unified inbox.
/// Тип определяет характер диалога и логику его отображения в интерфейсе команды.
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// Личная переписка в direct messages.
    /// </summary>
    DirectMessage,

    /// <summary>
    /// Ветвь комментариев, относящаяся к конкретной публикации.
    /// </summary>
    Comment,

    /// <summary>
    /// Ответ на упоминание аккаунта, например в stories или сходном формате платформы.
    /// </summary>
    MentionReply
}
