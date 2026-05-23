using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines persistence operations for workspace memberships and invitations.
/// </summary>
public interface IWorkspaceMemberRepository
{
    /// <summary>
    /// Retrieves a membership by its own identifier.
    /// </summary>
    /// <param name="id">Membership identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The tracked <see cref="WorkspaceMember"/> with related user data when available, or <c>null</c>.</returns>
    Task<WorkspaceMember?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists memberships of a workspace with user data when available.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An ordered list of memberships.</returns>
    Task<IReadOnlyList<WorkspaceMember>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Lists one page of memberships for a workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="skip">Number of records to skip.</param>
    /// <param name="take">Maximum number of records to return.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A page of memberships ordered by invitation timestamp.</returns>
    Task<IReadOnlyList<WorkspaceMember>> GetPagedByWorkspaceIdAsync(Guid workspaceId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Finds a membership for a specific user inside a specific workspace.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The matching membership, or <c>null</c>.</returns>
    Task<WorkspaceMember?> GetByUserAndWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Finds a membership invitation by the invited email address inside a workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="invitedEmail">Normalized invited email address.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The matching membership invitation, or <c>null</c>.</returns>
    Task<WorkspaceMember?> GetByInvitedEmailAsync(Guid workspaceId, string invitedEmail, CancellationToken ct = default);

    /// <summary>
    /// Lists active memberships for a specific user across all workspaces.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An ordered list of active memberships with workspace navigation loaded.</returns>
    Task<IReadOnlyList<WorkspaceMember>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Counts memberships of a workspace, optionally filtered by status.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of matching membership records.</returns>
    Task<int> CountByWorkspaceAsync(Guid workspaceId, MemberStatus? status = null, CancellationToken ct = default);

    /// <summary>
    /// Persists a new membership or invitation.
    /// </summary>
    /// <param name="member">Membership entity to add.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The same tracked <see cref="WorkspaceMember"/> entity.</returns>
    Task<WorkspaceMember> AddAsync(WorkspaceMember member, CancellationToken ct = default);

    /// <summary>
    /// Marks a tracked membership as modified.
    /// </summary>
    /// <param name="member">Membership entity carrying updated state.</param>
    void Update(WorkspaceMember member);

    /// <summary>
    /// Marks a membership for deletion.
    /// </summary>
    /// <param name="member">Membership entity to delete.</param>
    void Remove(WorkspaceMember member);

    /// <summary>
    /// Checks whether a user is already a member of a workspace.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> when the user already has a membership; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);
}
