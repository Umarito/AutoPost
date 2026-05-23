using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs.Notification;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  NOTIFICATION PREFERENCE DTOs — Per-User Notification Channel Settings     ║
// ║  TRD Stage 5: Notifications                                                ║
// ║  Endpoints: GET/PUT /api/notifications/preferences                         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for creating or updating a notification preference.
///
/// <para><b>What it does:</b>
/// Sets the notification channels (in-app, email, push) for a specific event type.
/// If a preference record doesn't exist yet for this event type, one is created.
/// If it already exists, it is updated with the new channel settings.</para>
///
/// <para><b>Upsert semantics:</b>
/// The service uses the composite key (UserId from context + WorkspaceId from context + EventType)
/// to determine whether to create or update. The caller only needs to specify the event type
/// and desired channel states.</para>
///
/// <para><b>TRD API:</b> PUT /api/notifications/preferences</para>
/// </summary>
/// <param name="EventType">The event type to configure: PostPublished, PostFailed, NewInboxMessage, AutomationTriggered, MemberInvited, etc.</param>
/// <param name="InAppEnabled">Whether to show in-app notifications (toast, badge, notification center) for this event.</param>
/// <param name="EmailEnabled">Whether to send email notifications for this event.</param>
/// <param name="PushEnabled">Whether to send push notifications (mobile/browser) for this event.</param>
public record UpdateNotificationPreferenceRequest(
    [Required] NotificationEventType EventType,
    bool InAppEnabled = true,
    bool EmailEnabled = true,
    bool PushEnabled = false
);

/// <summary>
/// Payload for bulk-updating all notification preferences at once.
///
/// <para><b>What it does:</b>
/// Allows the notification settings page to submit all preferences in a single request.
/// The service iterates through each item and applies upsert logic.</para>
///
/// <para><b>TRD API:</b> PUT /api/notifications/preferences/bulk</para>
/// </summary>
/// <param name="Preferences">List of preference updates to apply.</param>
public record BulkUpdateNotificationPreferencesRequest(
    string? Preset = null,
    List<UpdateNotificationPreferenceRequest>? Preferences = null
);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Notification preference for a single event type.
///
/// <para><b>Role in the system:</b>
/// Each record controls whether a specific event type triggers notifications through
/// in-app, email, and/or push channels. The notification dispatcher checks these
/// preferences before sending any notification.</para>
///
/// <para><b>Where it's used:</b>
/// Rendered as a row in the notification settings page: each event type has three
/// toggle switches (in-app, email, push) that the user can turn on/off.</para>
/// </summary>
/// <param name="Id">The preference record's unique identifier.</param>
/// <param name="EventType">The event type as string: "PostPublished", "PostFailed", "NewInboxMessage", etc.</param>
/// <param name="EventTypeDescription">Human-readable description of the event (e.g., "When a post is published successfully").</param>
/// <param name="InAppEnabled">Whether in-app notifications are enabled for this event.</param>
/// <param name="EmailEnabled">Whether email notifications are enabled for this event.</param>
/// <param name="PushEnabled">Whether push notifications are enabled for this event.</param>
public record NotificationPreferenceDto(
    Guid Id,
    string EventType,
    string EventTypeDescription,
    bool InAppEnabled,
    bool EmailEnabled,
    bool PushEnabled
);
