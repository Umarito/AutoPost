using Application.Abstractions.Notifications;

namespace Infrastructure.Notifications;

/// <summary>
/// Hangfire-job, который выполняет фактическую push-доставку вне HTTP-запроса.
/// </summary>
public sealed class SendPushNotificationJob
{
    private readonly PushNotificationTransport _pushNotificationTransport;

    /// <summary>
    /// Инициализирует job push-доставки.
    /// </summary>
    public SendPushNotificationJob(PushNotificationTransport pushNotificationTransport)
    {
        _pushNotificationTransport = pushNotificationTransport;
    }

    /// <summary>
    /// Отправляет push-уведомление через transport.
    /// </summary>
    public Task SendAsync(PushNotificationMessage message)
        => _pushNotificationTransport.SendNowAsync(message);
}
