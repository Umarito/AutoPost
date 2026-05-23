namespace Application.DTOs.Notification;

/// <summary>
/// Represents one notification that was generated or delivered to a user.
///
/// <para><b>Role in the system:</b>
/// This DTO backs notification center history, audit trails and diagnostics for
/// email, push and in-app notification delivery.</para>
/// </summary>
/// <param name="Id">Notification history record identifier.</param>
/// <param name="EventType">Logical event type that triggered the notification.</param>
/// <param name="Channel">Delivery channel such as InApp, Email or Push.</param>
/// <param name="Title">Short notification title.</param>
/// <param name="Body">Main notification body text.</param>
/// <param name="CreatedAt">UTC timestamp when the notification was generated.</param>
/// <param name="DeliveredAt">UTC timestamp when the notification was delivered, or <c>null</c> if still pending.</param>
public record NotificationHistoryDto(
    Guid Id,
    string EventType,
    string Channel,
    string Title,
    string Body,
    string? ActionUrl,
    DateTime CreatedAt,
    DateTime? DeliveredAt,
    bool IsDelivered,
    string? DeliveryError);
