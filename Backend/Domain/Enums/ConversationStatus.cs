namespace Domain.Enums;

/// <summary>
/// Рабочий статус диалога внутри unified inbox.
/// Помогает команде сортировать обращения и понимать, требуется ли дальнейшее действие.
/// </summary>
public enum ConversationStatus
{
    /// <summary>
    /// Диалог открыт и требует внимания команды.
    /// </summary>
    Open,

    /// <summary>
    /// Диалог обработан и считается завершенным.
    /// </summary>
    Resolved,

    /// <summary>
    /// Диалог временно отложен до более подходящего момента.
    /// </summary>
    Snoozed
}
