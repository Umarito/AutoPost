using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Notifications;

namespace Infrastructure.Notifications;

/// <summary>
/// Ставит push-уведомления в Hangfire-очередь, чтобы write-side команды не ждали сетевой доставки.
/// </summary>
public sealed class HangfirePushNotificationService : IPushNotificationService
{
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;

    /// <summary>
    /// Инициализирует очередь push-уведомлений.
    /// </summary>
    public HangfirePushNotificationService(IBackgroundJobScheduler backgroundJobScheduler)
    {
        _backgroundJobScheduler = backgroundJobScheduler;
    }

    /// <inheritdoc />
    public Task SendAsync(PushNotificationMessage notification, CancellationToken ct = default)
    {
        _backgroundJobScheduler.Enqueue<SendPushNotificationJob>(job => job.SendAsync(notification), "default");
        return Task.CompletedTask;
    }
}
