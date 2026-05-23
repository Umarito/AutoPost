using Application.Common;
using Application.DTOs.Workspace;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Workspace;

/// <summary>
/// Creates a new workspace for the current user.
/// </summary>
/// <param name="Name">Display name of the new workspace.</param>
/// <param name="Slug">URL-safe workspace slug that must remain unique.</param>
public sealed record CreateWorkspaceCommand(string Name, string Slug) : IRequest<Result<WorkspaceDto>>;

/// <summary>
/// Updates name or branding fields of an existing workspace.
/// </summary>
/// <param name="WorkspaceId">Workspace that should be updated.</param>
/// <param name="Request">Patch-style workspace update payload.</param>
public sealed record UpdateWorkspaceCommand(Guid WorkspaceId, UpdateWorkspaceRequest Request) : IRequest<Result<WorkspaceDto>>;

/// <summary>
/// Deactivates a workspace so it can no longer be used for publishing operations.
/// </summary>
/// <param name="WorkspaceId">Workspace that should be deactivated.</param>
public sealed record DeactivateWorkspaceCommand(Guid WorkspaceId) : IRequest<Result>;

/// <summary>
/// Invites a new member into the workspace with a preselected role.
/// </summary>
/// <param name="WorkspaceId">Workspace the invite belongs to.</param>
/// <param name="Request">Invitation payload containing target email and role.</param>
public sealed record InviteMemberCommand(Guid WorkspaceId, InviteMemberRequest Request) : IRequest<Result<MemberDto>>;

/// <summary>
/// Accepts a pending workspace invitation through a protected invite token.
/// </summary>
/// <param name="Token">Opaque invitation token sent in the invitation email.</param>
public sealed record AcceptInviteCommand(string Token) : IRequest<Result<MemberDto>>;

/// <summary>
/// Changes the workspace role of an existing member.
/// </summary>
/// <param name="WorkspaceId">Workspace that owns the membership.</param>
/// <param name="MemberId">Membership record to update.</param>
/// <param name="Role">New workspace role that should be assigned.</param>
public sealed record ChangeMemberRoleCommand(Guid WorkspaceId, Guid MemberId, WorkspaceRole Role) : IRequest<Result<MemberDto>>;

/// <summary>
/// Removes a member from a workspace.
/// </summary>
/// <param name="WorkspaceId">Workspace that owns the membership.</param>
/// <param name="MemberId">Membership record that should be removed.</param>
public sealed record RemoveMemberCommand(Guid WorkspaceId, Guid MemberId) : IRequest<Result>;

/// <summary>
/// Suspends a member without permanently deleting the membership record.
/// </summary>
/// <param name="WorkspaceId">Workspace that owns the membership.</param>
/// <param name="MemberId">Membership record that should be suspended.</param>
public sealed record SuspendMemberCommand(Guid WorkspaceId, Guid MemberId) : IRequest<Result<MemberDto>>;
