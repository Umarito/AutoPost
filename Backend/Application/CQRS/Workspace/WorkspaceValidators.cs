using Application.Common;
using Domain.Enums;
using FluentValidation;

namespace Application.CQRS.Workspace;

/// <summary>
/// Validates workspace creation input.
/// </summary>
public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CreateWorkspaceCommand"/>.
    /// </summary>
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(150);
    }
}

/// <summary>
/// Validates workspace update input.
/// </summary>
public sealed class UpdateWorkspaceCommandValidator : AbstractValidator<UpdateWorkspaceCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UpdateWorkspaceCommand"/>.
    /// </summary>
    public UpdateWorkspaceCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Name).MaximumLength(300).When(x => x.Request.Name is not null);
        RuleFor(x => x.Request.LogoUrl).MaximumLength(2048).When(x => x.Request.LogoUrl is not null);
    }
}

/// <summary>
/// Validates workspace deactivation input.
/// </summary>
public sealed class DeactivateWorkspaceCommandValidator : AbstractValidator<DeactivateWorkspaceCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DeactivateWorkspaceCommand"/>.
    /// </summary>
    public DeactivateWorkspaceCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

/// <summary>
/// Validates workspace invitation input.
/// </summary>
public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="InviteMemberCommand"/>.
    /// </summary>
    public InviteMemberCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.Role)
            .Must(role => role is WorkspaceRole.Admin or WorkspaceRole.Editor or WorkspaceRole.Viewer)
            .WithMessage("The invited role must be Admin, Editor or Viewer.");
    }
}

/// <summary>
/// Validates invitation acceptance input.
/// </summary>
public sealed class AcceptInviteCommandValidator : AbstractValidator<AcceptInviteCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="AcceptInviteCommand"/>.
    /// </summary>
    public AcceptInviteCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

/// <summary>
/// Validates role changes for existing members.
/// </summary>
public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ChangeMemberRoleCommand"/>.
    /// </summary>
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

/// <summary>
/// Validates member removal input.
/// </summary>
public sealed class RemoveMemberCommandValidator : AbstractValidator<RemoveMemberCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="RemoveMemberCommand"/>.
    /// </summary>
    public RemoveMemberCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

/// <summary>
/// Validates member suspension input.
/// </summary>
public sealed class SuspendMemberCommandValidator : AbstractValidator<SuspendMemberCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="SuspendMemberCommand"/>.
    /// </summary>
    public SuspendMemberCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

/// <summary>
/// Validates workspace lookup queries.
/// </summary>
public sealed class GetWorkspaceQueryValidator : AbstractValidator<GetWorkspaceQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetWorkspaceQuery"/>.
    /// </summary>
    public GetWorkspaceQueryValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

/// <summary>
/// Validates workspace usage lookup queries.
/// </summary>
public sealed class GetWorkspacePlanUsageQueryValidator : AbstractValidator<GetWorkspacePlanUsageQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetWorkspacePlanUsageQuery"/>.
    /// </summary>
    public GetWorkspacePlanUsageQueryValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

/// <summary>
/// Validates workspace member list queries.
/// </summary>
public sealed class GetWorkspaceMembersQueryValidator : AbstractValidator<GetWorkspaceMembersQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetWorkspaceMembersQuery"/>.
    /// </summary>
    public GetWorkspaceMembersQueryValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
