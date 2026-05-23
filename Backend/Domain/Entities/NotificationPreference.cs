using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Stores notification channel preferences for one user, one workspace and one event type.
/// </summary>
public class NotificationPreference : BaseEntity<Guid>
{
    /// <summary>
    /// Gets the owner user identifier of the preference.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the workspace identifier the preference belongs to.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the event type controlled by the preference.
    /// </summary>
    public NotificationEventType EventType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether in-app delivery is enabled.
    /// </summary>
    public bool InAppEnabled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether email delivery is enabled.
    /// </summary>
    public bool EmailEnabled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether push delivery is enabled.
    /// </summary>
    public bool PushEnabled { get; private set; }

    /// <summary>
    /// Gets the user navigation owning the preference.
    /// </summary>
    public ApplicationUser User { get; private set; } = default!;

    /// <summary>
    /// Creates a preference record with explicit channel settings.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="eventType">Event type controlled by the preference.</param>
    /// <param name="inAppEnabled">Whether in-app delivery is enabled.</param>
    /// <param name="emailEnabled">Whether email delivery is enabled.</param>
    /// <param name="pushEnabled">Whether push delivery is enabled.</param>
    /// <returns>A new <see cref="NotificationPreference"/> entity.</returns>
    public static NotificationPreference Create(
        Guid userId,
        Guid workspaceId,
        NotificationEventType eventType,
        bool inAppEnabled,
        bool emailEnabled,
        bool pushEnabled)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            EventType = eventType,
            InAppEnabled = inAppEnabled,
            EmailEnabled = emailEnabled,
            PushEnabled = pushEnabled
        };
    }

    /// <summary>
    /// Creates the default MVP notification set for a freshly onboarded workspace member.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <returns>A deterministic set of default preference records.</returns>
    public static IReadOnlyList<NotificationPreference> CreateDefaults(Guid userId, Guid workspaceId)
    {
        return Enum.GetValues<NotificationEventType>()
            .Select(eventType => Create(
                userId,
                workspaceId,
                eventType,
                inAppEnabled: true,
                emailEnabled: eventType is NotificationEventType.PostFailed or NotificationEventType.SocialAccountDisconnected or NotificationEventType.TeamMemberInvited,
                pushEnabled: false))
            .ToList();
    }

    /// <summary>
    /// Обновляет набор включенных каналов без изменения идентичности настройки.
    /// </summary>
    /// <param name="inAppEnabled">Новый флаг in-app канала.</param>
    /// <param name="emailEnabled">Новый флаг email канала.</param>
    /// <param name="pushEnabled">Новый флаг push канала.</param>
    public void UpdateChannels(bool inAppEnabled, bool emailEnabled, bool pushEnabled)
    {
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }
}
