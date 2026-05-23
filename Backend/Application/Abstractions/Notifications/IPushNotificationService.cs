namespace Application.Abstractions.Notifications;

/// <summary>
/// Sends push notifications to browser or mobile endpoints when a user has opted in.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Dispatches a push notification payload.
    /// </summary>
    /// <param name="notification">Prepared push payload including title, body and action metadata.</param>
    /// <param name="ct">Cancellation token for outbound provider I/O.</param>
    /// <returns>A task that completes when the notification has been handed off to the push provider.</returns>
    Task SendAsync(PushNotificationMessage notification, CancellationToken ct = default);
}
