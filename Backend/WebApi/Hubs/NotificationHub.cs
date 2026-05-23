using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace WebApi.Hubs;

/// <summary>
/// Provides the real-time notification channel used by the AutoPost frontend.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    /// <summary>
    /// Adds the connecting client to user-specific and workspace-specific groups.
    /// </summary>
    /// <returns>A task that completes after the connection has been grouped.</returns>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        var workspaceId = Context.User?.FindFirstValue("workspace_id");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
        }

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetWorkspaceGroup(workspaceId));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Builds the SignalR group name used for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The group name used by the hub layer.</returns>
    public static string GetUserGroup(string userId) => $"user:{userId}";

    /// <summary>
    /// Builds the SignalR group name used for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The group name used by the hub layer.</returns>
    public static string GetWorkspaceGroup(string workspaceId) => $"workspace:{workspaceId}";
}
