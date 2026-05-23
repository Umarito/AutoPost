using Application.Common;
using Application.DTOs.Workspace;
using MediatR;

namespace Application.CQRS.Workspace;

/// <summary>
/// Retrieves one workspace by its identifier.
/// </summary>
/// <param name="WorkspaceId">Workspace identifier to load.</param>
public sealed record GetWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<WorkspaceDto>>;

/// <summary>
/// Retrieves current plan usage for a workspace.
/// </summary>
/// <param name="WorkspaceId">Workspace whose usage should be summarized.</param>
public sealed record GetWorkspacePlanUsageQuery(Guid WorkspaceId) : IRequest<Result<WorkspacePlanUsageDto>>;

/// <summary>
/// Retrieves a paginated list of workspace members.
/// </summary>
/// <param name="WorkspaceId">Workspace whose members should be listed.</param>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetWorkspaceMembersQuery(Guid WorkspaceId, PagedRequest Pagination) : IRequest<Result<PagedResult<MemberDto>>>;
