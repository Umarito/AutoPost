using Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

/// <summary>
/// Выполняет фактическую push-доставку.
/// Текущий MVP использует logging-based fallback, сохраняя совместимый контракт для последующего подключения реального провайдера.
/// </summary>
public sealed class PushNotificationTransport
{
    private readonly ILogger<PushNotificationTransport> _logger;

    /// <summary>
    /// Инициализирует транспорт push-уведомлений.
    /// </summary>
    public PushNotificationTransport(ILogger<PushNotificationTransport> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Выполняет немедленную отправку push-уведомления.
    /// </summary>
    public Task SendNowAsync(PushNotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Push notification accepted by fallback transport for user {UserId} with title {Title}.",
            message.UserId,
            message.Title);

        return Task.CompletedTask;
    }
}
