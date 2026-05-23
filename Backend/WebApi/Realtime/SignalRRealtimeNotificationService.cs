using Application.Abstractions.Notifications;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Realtime;

/// <summary>
/// Sends application notifications through SignalR hubs.
/// </summary>
public sealed class SignalRRealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    /// <summary>
    /// Initializes the notification dispatcher with the SignalR hub context.
    /// </summary>
    /// <param name="hubContext">The hub context used to send server-initiated messages.</param>
    public SignalRRealtimeNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task NotifyUserAsync(Guid userId, string eventName, object? payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(NotificationHub.GetUserGroup(userId.ToString()))
            .SendAsync(eventName, payload, ct);

    /// <inheritdoc />
    public Task NotifyWorkspaceAsync(Guid workspaceId, string eventName, object? payload, CancellationToken ct = default)
        => _hubContext.Clients.Group(NotificationHub.GetWorkspaceGroup(workspaceId.ToString()))
            .SendAsync(eventName, payload, ct);
}
