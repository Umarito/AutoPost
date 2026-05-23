using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents one user's membership inside a workspace.
/// </summary>
public class WorkspaceMember : BaseEntity<Guid>
{
    /// <summary>
    /// Gets the workspace identifier that owns the membership.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the bound user identifier, or <c>null</c> while the invitation is still pending registration.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Gets the role assigned inside the workspace.
    /// </summary>
    public WorkspaceRole Role { get; private set; }

    /// <summary>
    /// Gets the current lifecycle status of the membership.
    /// </summary>
    public MemberStatus Status { get; private set; }

    /// <summary>
    /// Gets the invitation email tied to the membership.
    /// </summary>
    public string InvitedEmail { get; private set; } = default!;

    /// <summary>
    /// Gets the inviter user identifier, if the invite was created by a user.
    /// </summary>
    public Guid? InvitedByUserId { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the invite or owner membership was created.
    /// </summary>
    public DateTime InvitedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the membership became active, if any.
    /// </summary>
    public DateTime? JoinedAt { get; private set; }

    /// <summary>
    /// Gets the parent workspace navigation.
    /// </summary>
    public Workspace Workspace { get; private set; } = default!;

    /// <summary>
    /// Gets the bound user navigation, if the invitation has been accepted.
    /// </summary>
    public ApplicationUser? User { get; private set; }

    /// <summary>
    /// Gets the inviter user navigation.
    /// </summary>
    public ApplicationUser? InvitedBy { get; private set; }

    /// <summary>
    /// Creates the initial owner membership for a newly created workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="ownerEmail">Owner email for audit trail consistency.</param>
    /// <param name="createdAtUtc">UTC creation timestamp.</param>
    /// <returns>A fully initialized owner membership.</returns>
    public static WorkspaceMember CreateOwner(
        Guid workspaceId,
        Guid userId,
        string ownerEmail,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerEmail);

        return new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            Status = MemberStatus.Active,
            InvitedEmail = ownerEmail.Trim(),
            InvitedAt = createdAtUtc,
            JoinedAt = createdAtUtc
        };
    }

    /// <summary>
    /// Creates a pending invitation membership.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="email">Invitation target email.</param>
    /// <param name="role">Invited role.</param>
    /// <param name="invitedByUserId">Inviter user identifier.</param>
    /// <param name="invitedAtUtc">UTC invitation timestamp.</param>
    /// <param name="existingUserId">Existing registered user identifier if the target email already belongs to a user.</param>
    /// <returns>A new invitation membership.</returns>
    public static WorkspaceMember CreateInvitation(
        Guid workspaceId,
        string email,
        WorkspaceRole role,
        Guid invitedByUserId,
        DateTime invitedAtUtc,
        Guid? existingUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = existingUserId,
            Role = role,
            Status = MemberStatus.Invited,
            InvitedEmail = email.Trim(),
            InvitedByUserId = invitedByUserId,
            InvitedAt = invitedAtUtc
        };
    }

    /// <summary>
    /// Activates a pending invitation for a concrete user account.
    /// </summary>
    /// <param name="userId">Registered user identifier accepting the invitation.</param>
    /// <param name="joinedAtUtc">UTC acceptance timestamp.</param>
    public void Accept(Guid userId, DateTime joinedAtUtc)
    {
        UserId = userId;
        Status = MemberStatus.Active;
        JoinedAt = joinedAtUtc;
    }

    /// <summary>
    /// Changes the effective workspace role.
    /// </summary>
    /// <param name="role">New workspace role.</param>
    public void ChangeRole(WorkspaceRole role)
    {
        Role = role;
    }

    /// <summary>
    /// Suspends the membership without deleting the audit trail.
    /// </summary>
    public void Suspend()
    {
        Status = MemberStatus.Suspended;
    }
}
