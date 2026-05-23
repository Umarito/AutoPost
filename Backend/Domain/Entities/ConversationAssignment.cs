using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Назначение диалога конкретному сотруднику команды.
/// Сущность позволяет распределять ответственность за обработку conversations между участниками workspace.
/// </summary>
public class ConversationAssignment : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новое назначение ответственного на диалог.
    /// </summary>
    /// <param name="conversationId">Идентификатор диалога.</param>
    /// <param name="assignedToUserId">Идентификатор ответственного пользователя.</param>
    /// <param name="assignedByUserId">Идентификатор назначившего пользователя.</param>
    /// <param name="assignedAtUtc">UTC-время назначения.</param>
    /// <param name="note">Опциональная служебная заметка.</param>
    /// <returns>Новая сущность назначения.</returns>
    public static ConversationAssignment Create(
        Guid conversationId,
        Guid assignedToUserId,
        Guid? assignedByUserId,
        DateTime assignedAtUtc,
        string? note)
    {
        return new ConversationAssignment
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            AssignedToUserId = assignedToUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = assignedAtUtc,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
    }

    /// <summary>
    /// Идентификатор диалога, которому назначается ответственный.
    /// Поле связывает распределение нагрузки с конкретной conversation в unified inbox.
    /// </summary>
    public Guid ConversationId { get; private set; }

    /// <summary>
    /// Идентификатор пользователя, назначенного ответственным за диалог.
    /// Это основной исполнитель, который должен обработать входящее обращение.
    /// </summary>
    public Guid AssignedToUserId { get; private set; }

    /// <summary>
    /// Идентификатор пользователя, выполнившего назначение.
    /// Значение может отсутствовать, если распределение было сделано автоматически системой.
    /// </summary>
    public Guid? AssignedByUserId { get; private set; }

    /// <summary>
    /// Дата и время назначения.
    /// Поле используется для аудита, контроля скорости маршрутизации и истории изменения ответственных.
    /// </summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// Дополнительная заметка к назначению.
    /// Значение помогает передать контекст: приоритет, тип клиента или причины маршрутизации.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Диалог, на который назначается сотрудник.
    /// Навигация связывает маршрут обработки с конкретной conversation.
    /// </summary>
    public InboxConversation Conversation { get; private set; } = default!;

    /// <summary>
    /// Пользователь, назначенный ответственным за диалог.
    /// Навигация позволяет быстро получить сведения о менеджере, обрабатывающем conversation.
    /// </summary>
    public ApplicationUser AssignedTo { get; private set; } = default!;

    /// <summary>
    /// Пользователь, инициировавший назначение.
    /// Навигация нужна для истории командных действий и может отсутствовать при автоматической маршрутизации.
    /// </summary>
    public ApplicationUser? AssignedBy { get; private set; }
}
