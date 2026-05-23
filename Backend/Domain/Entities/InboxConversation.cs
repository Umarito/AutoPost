using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Единица общения в unified inbox.
/// Сущность агрегирует поток сообщений или комментариев от одного внешнего пользователя в рамках конкретного канала.
/// </summary>
public class InboxConversation : BaseEntity<Guid>
{
    /// <summary>
    /// Создает новый диалог unified inbox.
    /// </summary>
    /// <param name="workspaceId">Идентификатор рабочего пространства.</param>
    /// <param name="socialAccountId">Идентификатор социального аккаунта.</param>
    /// <param name="type">Тип диалога.</param>
    /// <param name="externalConversationId">Внешний идентификатор диалога.</param>
    /// <param name="externalUserId">Внешний идентификатор пользователя.</param>
    /// <param name="externalUserName">Имя внешнего пользователя.</param>
    /// <param name="externalUserAvatarUrl">Аватар внешнего пользователя.</param>
    /// <param name="createdAtUtc">UTC-время создания.</param>
    /// <returns>Новая conversation-сущность.</returns>
    public static InboxConversation Create(
        Guid workspaceId,
        Guid socialAccountId,
        ConversationType type,
        string externalConversationId,
        string externalUserId,
        string? externalUserName,
        string? externalUserAvatarUrl,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalConversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalUserId);

        return new InboxConversation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SocialAccountId = socialAccountId,
            Type = type,
            ExternalConversationId = externalConversationId.Trim(),
            ExternalUserId = externalUserId.Trim(),
            ExternalUserName = string.IsNullOrWhiteSpace(externalUserName) ? null : externalUserName.Trim(),
            ExternalUserAvatarUrl = string.IsNullOrWhiteSpace(externalUserAvatarUrl) ? null : externalUserAvatarUrl.Trim(),
            Status = ConversationStatus.Open,
            UnreadCount = 0,
            CreatedAt = createdAtUtc
        };
    }

    /// <summary>
    /// Идентификатор рабочего пространства, которому принадлежит диалог.
    /// Поле обеспечивает tenant-изоляцию переписки и ее доступность только для нужной команды.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Идентификатор подключенного социального аккаунта, через который идет общение.
    /// Значение определяет внешний канал, к которому привязана переписка.
    /// </summary>
    public Guid SocialAccountId { get; private set; }

    /// <summary>
    /// Тип диалога, например direct message, комментарий или ответ на упоминание.
    /// Это поле определяет характер коммуникации и влияет на сценарии работы в inbox.
    /// </summary>
    public ConversationType Type { get; private set; }

    /// <summary>
    /// Идентификатор диалога на стороне платформы.
    /// Он нужен для дедупликации webhook-событий и корректной синхронизации сообщений.
    /// </summary>
    public string ExternalConversationId { get; private set; } = default!;

    /// <summary>
    /// Идентификатор внешнего пользователя, участвующего в диалоге.
    /// Поле позволяет связывать множество сообщений и событий с одним собеседником.
    /// </summary>
    public string ExternalUserId { get; private set; } = default!;

    /// <summary>
    /// Username или отображаемое имя внешнего пользователя.
    /// Значение используется командой для распознавания собеседника в интерфейсе inbox.
    /// </summary>
    public string? ExternalUserName { get; private set; }

    /// <summary>
    /// Ссылка на аватар внешнего пользователя.
    /// Поле помогает визуально различать собеседников при работе с большим числом переписок.
    /// </summary>
    public string? ExternalUserAvatarUrl { get; private set; }

    /// <summary>
    /// Признак того, что внешний пользователь подписан на наш аккаунт.
    /// Значение критично для сценариев, где возможность отправки DM зависит от статуса подписки.
    /// </summary>
    public bool IsFollowingUs { get; private set; }

    /// <summary>
    /// Время последней проверки статуса подписки в UTC.
    /// Метка помогает понимать свежесть данных, влияющих на логику automation и отложенных сообщений.
    /// </summary>
    public DateTime? IsFollowingUsCheckedAt { get; private set; }

    /// <summary>
    /// Идентификатор целевой публикации, если разговор относится к цепочке комментариев под конкретным постом.
    /// Для личной переписки значение обычно отсутствует.
    /// </summary>
    public Guid? PostTargetId { get; private set; }

    /// <summary>
    /// Идентификатор поста на стороне платформы, к которому относится коммуникация.
    /// Поле полезно для построения ссылок и контекстного просмотра комментариев.
    /// </summary>
    public string? ExternalPostId { get; private set; }

    /// <summary>
    /// Краткий текст или превью последнего сообщения в диалоге.
    /// Используется для быстрого обзора переписки в списке conversations.
    /// </summary>
    public string? LastMessagePreview { get; private set; }

    /// <summary>
    /// Время последнего сообщения в диалоге.
    /// Значение помогает сортировать conversation list по актуальности и приоритету обработки.
    /// </summary>
    public DateTime? LastMessageAt { get; private set; }

    /// <summary>
    /// Рабочий статус диалога для команды.
    /// Поле используется для операционного управления перепиской: открыта ли она, закрыта или отложена.
    /// </summary>
    public ConversationStatus Status { get; private set; }

    /// <summary>
    /// Количество сообщений, которые еще не были просмотрены командой.
    /// Счетчик нужен для бейджей, фильтров и оценки текущей очереди обращений.
    /// </summary>
    public int UnreadCount { get; private set; }

    /// <summary>
    /// Дата и время создания диалога в системе.
    /// Поле фиксирует момент первого появления conversation в unified inbox.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Рабочее пространство, которому принадлежит диалог.
    /// Навигация связывает conversation с tenant-контекстом и доступом команды.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Социальный аккаунт, через который ведется коммуникация.
    /// Навигация связывает диалог с конкретным внешним каналом общения.
    /// </summary>
    public SocialAccount SocialAccount { get; private set; } = default!;

    /// <summary>
    /// Целевая публикация, к которой относится данная conversation в комментариях.
    /// Навигация может отсутствовать, если речь идет не о публичном обсуждении поста.
    /// </summary>
    public PostTarget? PostTarget { get; private set; }

    /// <summary>
    /// Сообщения, входящие в текущий диалог.
    /// Коллекция представляет полную хронологию общения с внешним пользователем или под конкретным постом.
    /// </summary>
    public IReadOnlyCollection<InboxMessage> Messages { get; private set; } = new List<InboxMessage>();

    /// <summary>
    /// Текущее назначение ответственного сотрудника на диалог.
    /// Навигация помогает организовать распределение входящих обращений по команде.
    /// </summary>
    public ConversationAssignment? Assignment { get; private set; }

    /// <summary>
    /// Обновляет сведения о внешнем пользователе, если платформа прислала более свежие данные.
    /// </summary>
    /// <param name="externalUserName">Новое отображаемое имя.</param>
    /// <param name="externalUserAvatarUrl">Новый URL аватара.</param>
    public void UpdateParticipant(string? externalUserName, string? externalUserAvatarUrl)
    {
        ExternalUserName = string.IsNullOrWhiteSpace(externalUserName) ? ExternalUserName : externalUserName.Trim();
        ExternalUserAvatarUrl = string.IsNullOrWhiteSpace(externalUserAvatarUrl) ? ExternalUserAvatarUrl : externalUserAvatarUrl.Trim();
    }

    /// <summary>
    /// Привязывает диалог к целевой публикации и внешнему посту, если речь идет о ветке комментариев.
    /// </summary>
    /// <param name="postTargetId">Идентификатор целевой публикации.</param>
    /// <param name="externalPostId">Внешний идентификатор поста.</param>
    public void AttachToPost(Guid? postTargetId, string? externalPostId)
    {
        PostTargetId = postTargetId;
        ExternalPostId = string.IsNullOrWhiteSpace(externalPostId) ? null : externalPostId.Trim();
    }

    /// <summary>
    /// Обновляет информацию о возможности direct-message контакта.
    /// </summary>
    /// <param name="isFollowingUs">Актуальный признак подписки внешнего пользователя.</param>
    /// <param name="checkedAtUtc">UTC-время проверки.</param>
    public void UpdateFollowState(bool isFollowingUs, DateTime checkedAtUtc)
    {
        IsFollowingUs = isFollowingUs;
        IsFollowingUsCheckedAt = checkedAtUtc;
    }

    /// <summary>
    /// Регистрирует новое входящее сообщение и повышает приоритет обработки диалога.
    /// </summary>
    /// <param name="preview">Превью сообщения для списка диалогов.</param>
    /// <param name="messageAtUtc">UTC-время последнего сообщения.</param>
    public void RegisterInboundMessage(string? preview, DateTime messageAtUtc)
    {
        LastMessagePreview = string.IsNullOrWhiteSpace(preview) ? LastMessagePreview : preview.Trim();
        LastMessageAt = messageAtUtc;
        UnreadCount++;
        Status = ConversationStatus.Open;
    }

    /// <summary>
    /// Регистрирует исходящее сообщение, не увеличивая счетчик непрочитанных входящих.
    /// </summary>
    /// <param name="preview">Превью сообщения.</param>
    /// <param name="messageAtUtc">UTC-время исходящего сообщения.</param>
    public void RegisterOutboundMessage(string? preview, DateTime messageAtUtc)
    {
        LastMessagePreview = string.IsNullOrWhiteSpace(preview) ? LastMessagePreview : preview.Trim();
        LastMessageAt = messageAtUtc;
    }

    /// <summary>
    /// Меняет рабочий статус диалога.
    /// </summary>
    /// <param name="status">Новый статус.</param>
    public void ChangeStatus(ConversationStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// Сбрасывает счетчик непрочитанных входящих сообщений после просмотра диалога командой.
    /// </summary>
    public void ClearUnread()
    {
        UnreadCount = 0;
    }
}
