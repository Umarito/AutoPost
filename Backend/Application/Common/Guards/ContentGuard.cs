using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Guards;

/// <summary>
/// Provides reusable guard clauses for content-pipeline handlers.
/// </summary>
internal static class ContentGuard
{
    /// <summary>
    /// Ensures the current user has at least read access to the target workspace.
    /// </summary>
    /// <param name="userId">Authenticated application user identifier.</param>
    /// <param name="workspaceId">Workspace that should be accessed.</param>
    /// <param name="workspaceMemberRepository">Membership repository used for DB-backed authorization.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A successful result that contains the active membership when authorization succeeds,
    /// or a failed result describing why access is denied.
    /// </returns>
    public static async Task<Result<WorkspaceMember>> RequireReadAccessAsync(
        Guid userId,
        Guid workspaceId,
        IWorkspaceMemberRepository workspaceMemberRepository,
        CancellationToken ct)
    {
        var membership = await workspaceMemberRepository.GetByUserAndWorkspaceAsync(userId, workspaceId, ct);
        if (membership is null || membership.Status != MemberStatus.Active)
        {
            return Result<WorkspaceMember>.Fail("Access to the requested workspace is forbidden.", ErrorCode.Forbidden);
        }

        return Result<WorkspaceMember>.Ok(membership);
    }

    /// <summary>
    /// Ensures the current user can modify content within the target workspace.
    /// </summary>
    /// <param name="userId">Authenticated application user identifier.</param>
    /// <param name="workspaceId">Workspace that should be modified.</param>
    /// <param name="workspaceMemberRepository">Membership repository used for DB-backed authorization.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A successful result that contains the active membership when the user can write content,
    /// or a failed result describing why access is denied.
    /// </returns>
    public static async Task<Result<WorkspaceMember>> RequireContentWriteAccessAsync(
        Guid userId,
        Guid workspaceId,
        IWorkspaceMemberRepository workspaceMemberRepository,
        CancellationToken ct)
    {
        var access = await RequireReadAccessAsync(userId, workspaceId, workspaceMemberRepository, ct);
        if (!access.IsSuccess)
        {
            return access;
        }

        if (access.Value!.Role is WorkspaceRole.Viewer)
        {
            return Result<WorkspaceMember>.Fail("Content write permissions are required for this workspace.", ErrorCode.Forbidden);
        }

        return access;
    }

    /// <summary>
    /// Ensures the current user can manage integrations and scheduling in the target workspace.
    /// </summary>
    /// <param name="userId">Authenticated application user identifier.</param>
    /// <param name="workspaceId">Workspace that should be managed.</param>
    /// <param name="workspaceMemberRepository">Membership repository used for DB-backed authorization.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A successful result that contains the active membership when the user has management permissions,
    /// or a failed result describing why access is denied.
    /// </returns>
    public static async Task<Result<WorkspaceMember>> RequireManagementAccessAsync(
        Guid userId,
        Guid workspaceId,
        IWorkspaceMemberRepository workspaceMemberRepository,
        CancellationToken ct)
    {
        var access = await RequireReadAccessAsync(userId, workspaceId, workspaceMemberRepository, ct);
        if (!access.IsSuccess)
        {
            return access;
        }

        if (access.Value!.Role is not WorkspaceRole.Owner and not WorkspaceRole.Admin)
        {
            return Result<WorkspaceMember>.Fail("Workspace management permissions are required.", ErrorCode.Forbidden);
        }

        return access;
    }

    /// <summary>
    /// Creates a standardized not-found result for query handlers.
    /// </summary>
    /// <typeparam name="T">Expected success payload type.</typeparam>
    /// <param name="entityName">Human-readable entity name used in the message.</param>
    /// <returns>A failed result with <see cref="ErrorCode.NotFound"/>.</returns>
    public static Result<T> NotFound<T>(string entityName)
        => Result<T>.Fail($"{entityName} was not found.", ErrorCode.NotFound);

    /// <summary>
    /// Creates a standardized not-found result for command handlers.
    /// </summary>
    /// <param name="entityName">Human-readable entity name used in the message.</param>
    /// <returns>A failed result with <see cref="ErrorCode.NotFound"/>.</returns>
    public static Result NotFound(string entityName)
        => Result.Fail($"{entityName} was not found.", ErrorCode.NotFound);
}
