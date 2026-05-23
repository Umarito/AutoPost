namespace Application.Abstractions.Notifications;

/// <summary>
/// Publishes real-time notifications to connected clients.
///
/// <para>
/// The Application layer can depend on this contract when it needs to notify
/// users or workspace members about inbox updates, publishing progress or
/// other domain events without referencing SignalR directly.
/// </para>
/// </summary>
public interface IRealtimeNotificationService
{
    /// <summary>
    /// Sends a named event with an optional payload to a specific user.
    /// </summary>
    /// <param name="userId">The application user identifier that should receive the event.</param>
    /// <param name="eventName">The logical event name consumed by the client.</param>
    /// <param name="payload">The payload object to serialize and deliver.</param>
    /// <param name="ct">The cancellation token for the async send operation.</param>
    Task NotifyUserAsync(Guid userId, string eventName, object? payload, CancellationToken ct = default);

    /// <summary>
    /// Sends a named event with an optional payload to all connections grouped by workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier whose group should receive the event.</param>
    /// <param name="eventName">The logical event name consumed by the client.</param>
    /// <param name="payload">The payload object to serialize and deliver.</param>
    /// <param name="ct">The cancellation token for the async send operation.</param>
    Task NotifyWorkspaceAsync(Guid workspaceId, string eventName, object? payload, CancellationToken ct = default);
}
