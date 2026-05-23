using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="NotificationPreference"/> entity.
///
/// <para><b>Role in the system:</b>
/// NotificationPreference stores per-user, per-workspace, per-event notification channel settings.
/// Each record controls whether a specific event type (e.g., PostPublished, NewInboxMessage)
/// triggers in-app, email, and/or push notifications. This lets users fine-tune their notification
/// experience without affecting their teammates.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 5 — Notifications. "InAppEnabled, EmailEnabled, PushEnabled per event type."</para>
/// </summary>
public interface INotificationPreferenceRepository
{
    /// <summary>
    /// Retrieves all notification preferences for a user within a specific workspace.
    /// Used to render the notification settings page where the user toggles
    /// channels for each event type. Results are not tracked (read-only).
    /// </summary>
    Task<IReadOnlyList<NotificationPreference>> GetByUserAndWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new notification preference record.
    /// Called when a user customizes a notification channel for an event type that
    /// doesn't yet have a preference record (first-time customization).
    /// </summary>
    Task<NotificationPreference> AddAsync(NotificationPreference preference, CancellationToken ct = default);

    /// <summary>
    /// Marks a preference as modified. Called when the user toggles a channel
    /// (e.g., turns off email for PostFailed events).
    /// </summary>
    void Update(NotificationPreference preference);

    /// <summary>
    /// Retrieves the preference for a specific user + workspace + event type combination.
    /// Used by the notification dispatcher to check whether to send a notification
    /// through a specific channel before actually sending it.
    /// Returns <c>null</c> if the user hasn't customized this event type (use defaults).
    /// </summary>
    Task<NotificationPreference?> GetByUserWorkspaceAndEventAsync(Guid userId, Guid workspaceId, NotificationEventType eventType, CancellationToken ct = default);
}
