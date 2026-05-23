using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs.Workspace;

/// <summary>
/// Payload for updating workspace settings.
/// </summary>
/// <param name="Name">New workspace display name.</param>
/// <param name="LogoUrl">URL to the workspace logo image.</param>
public record UpdateWorkspaceRequest(
    [MaxLength(300)] string? Name,
    [MaxLength(2048)] string? LogoUrl
);

/// <summary>
/// Payload for inviting a new member to the workspace.
/// </summary>
/// <param name="Email">Email address of the invited person.</param>
/// <param name="Role">Role assigned after the invitation is accepted.</param>
public record InviteMemberRequest(
    [Required, EmailAddress] string Email,
    [Required] WorkspaceRole Role
);

/// <summary>
/// Workspace data returned by workspace endpoints.
/// </summary>
/// <param name="Id">The workspace identifier.</param>
/// <param name="Name">The display name of the workspace.</param>
/// <param name="Slug">The globally unique URL slug.</param>
/// <param name="LogoUrl">The optional logo URL.</param>
/// <param name="Plan">The current plan name.</param>
/// <param name="MaxSocialAccounts">Maximum social accounts allowed by the plan.</param>
/// <param name="MaxMembersPerWorkspace">Maximum team members allowed by the plan.</param>
/// <param name="MaxPostsPerMonth">Maximum monthly posts allowed by the plan according to the current plan catalog.</param>
/// <param name="IsActive">Whether the workspace is active.</param>
/// <param name="MemberCount">Current number of members.</param>
/// <param name="CreatedAt">UTC workspace creation timestamp.</param>
public record WorkspaceDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Plan,
    int MaxSocialAccounts,
    int MaxMembersPerWorkspace,
    int MaxPostsPerMonth,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt
);

/// <summary>
/// Workspace member data returned by team-management endpoints.
/// </summary>
/// <param name="Id">Membership record identifier.</param>
/// <param name="UserId">Bound user identifier, or <c>null</c> while the invite is still pending registration.</param>
/// <param name="DisplayName">Display name shown in team lists.</param>
/// <param name="Email">Effective email bound to the membership or pending invitation.</param>
/// <param name="AvatarUrl">Optional avatar URL of the bound user.</param>
/// <param name="Role">Workspace role serialized as a string.</param>
/// <param name="Status">Membership lifecycle status serialized as a string.</param>
/// <param name="JoinedAt">UTC timestamp when the invitation was accepted, or <c>null</c> while still pending.</param>
public record MemberDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTime? JoinedAt
);
