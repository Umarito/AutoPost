// WebApi/Controllers/WorkspaceController.cs
using Application.Common;
using Application.CQRS.Workspace;
using Application.DTOs.Workspace;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApi.Controllers;

/// <summary>
/// Manages tenant workspaces, billing plan usage, and team collaboration memberships.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Workspace module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Workspace is the tenant isolation boundary. This controller allows owners and admins to manage workspace properties and team permissions.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Invokes CQRS commands and queries via MediatR. Ensures actions align with the client session context provided by <see cref="ICurrentUserContext"/>.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Prevents IDOR (Insecure Direct Object Reference) vectors by enforcing strict claims validation. Implements policy-based authorization limits.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class WorkspaceController : ApiControllerBase
{
    private readonly ICurrentUserContext _currentUserContext;

    /// <summary>
    /// Initializes the WorkspaceController.
    /// </summary>
    /// <param name="currentUserContext">Access to current user and active workspace claims.</param>
    public WorkspaceController(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    /// <summary>
    /// Creates a new workspace for the authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/workspace - Tenant creation route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to self-provision additional workspaces for different teams or social media brands.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates input, dispatches <see cref="CreateWorkspaceCommand"/>, registers the user as Owner, and returns workspace metadata.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Automatically isolates data under the new workspace partition. Limits mass-assignment by utilizing a local record request.</para>
    /// </remarks>
    /// <param name="request">Payload containing name and unique url-friendly slug.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created workspace representation.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspaceDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var command = new CreateWorkspaceCommand(request.Name, request.Slug);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the details of a workspace by its unique identifier.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/workspace/{id} - Workspace details fetch.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Standard dashboard query to load workspace configurations.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Cross-checks requested ID, dispatches <see cref="GetWorkspaceQuery"/>, and projects the repository model to a DTO.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Implements defensive AppSec check: denies access if the requested ID does not match the active session workspace claim.</para>
    /// </remarks>
    /// <param name="id">The workspace identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The matching workspace details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspaceDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var query = new GetWorkspaceQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates settings for a workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/workspace/{id} - Settings modification.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows team admins to rename workspaces or update branding logos.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Checks claims, routes payload via <see cref="UpdateWorkspaceCommand"/>, and commits changes to DB.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Secured via <c>WorkspaceAdmin</c> policy limits to protect tenant boundaries from unauthorized members.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="request">Payload containing name and logo options.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated workspace representation.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspaceDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new UpdateWorkspaceCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Deactivates a workspace, disabling all scheduled publishing operations.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/workspace/{id} - Workspace deactivation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Provides self-service de-provisioning of teams on subscription cancellation.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates owner credentials, dispatches <see cref="DeactivateWorkspaceCommand"/>, updates active flags, and flushes tokens.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly guarded by the <c>WorkspaceOwner</c> authorization policy constraint.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Policy = "WorkspaceOwner")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new DeactivateWorkspaceCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves current plan limits and publishing usage quotas for the workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/workspace/{id}/plan-usage - Subscription quota metrics.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Essential for the UI dashboard to show users how many social accounts and monthly posts remain.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Dispatches <see cref="GetWorkspacePlanUsageQuery"/>, fetches count indicators, and checks limits.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Safeguards resource consumption. Leverages Redis-cached counts to maximize query throughput.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Current subscription utilization details.</returns>
    [HttpGet("{id:guid}/plan-usage")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkspacePlanUsageDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetPlanUsage(Guid id, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var query = new GetWorkspacePlanUsageQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists all members and pending invitations within the workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/workspace/{id}/members - Member lookup route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables team management dashboards to render member roles and invitation states.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates access, handles pagination, executes <see cref="GetWorkspaceMembersQuery"/>, and projects profiles.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Blocks IDOR. Leverages cached queries to protect relational indexes.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="pageSize">Size of the requested data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of workspace members.</returns>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<MemberDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetMembers(Guid id, [FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetWorkspaceMembersQuery(id, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Invites a new team member by email to join the workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/workspace/{id}/members/invite - Team member invitation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Core workspace collaboration mechanism. Dispatches invitation link.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Confirms admin rights, builds invitation tokens, adds pending members to DB, and sends email.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restricts mass email spamming using rate limits. Input sanitized to prevent script injection.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="request">Payload containing recipient email and role mappings.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created pending member detail representation.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpPost("{id:guid}/members/invite")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MemberDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> InviteMember(Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new InviteMemberCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Accepts a pending invitation using the token dispatched in the invitation email.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/workspace/members/accept-invite - User invitation onboarding.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables external partners to securely self-register or join existing workspaces.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Does not require workspace headers. Validates token, connects user identity, and moves status to Active.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly validates token lifetime (default 48h) to prevent invitation reuse or replay attacks.</para>
    /// </remarks>
    /// <param name="request">Payload containing the opaque acceptance token.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly activated membership details.</returns>
    [HttpPost("members/accept-invite")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MemberDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var command = new AcceptInviteCommand(request.Token);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Modifies the access role of a member in the workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/workspace/{id}/members/{memberId}/role - Role adjustment.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables workspace admins to promote members or restrict guest access.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Confirms claims, validates target role transition, updates DB state, and invalidates session cache.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarded by <c>WorkspaceAdmin</c> role policy. Blocks non-owners from modifying owner roles.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="memberId">Membership record identifier.</param>
    /// <param name="request">Payload mapping the new workspace role.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated membership details.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpPut("{id:guid}/members/{memberId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MemberDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ChangeMemberRole(Guid id, Guid memberId, [FromBody] ChangeMemberRoleRequest request, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new ChangeMemberRoleCommand(id, memberId, request.Role);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Removes a member from the workspace team.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/workspace/{id}/members/{memberId} - Membership termination.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Revokes access for employees offboarding from the team.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Triggers <see cref="RemoveMemberCommand"/>, checks authorization hierarchies, deletes membership, and purges refresh tokens.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Revokes workspace claims instantly. Prevents IDOR by validating workspace context matching.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="memberId">Membership record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new RemoveMemberCommand(id, memberId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Temporarily suspends a member's access to the workspace operations.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/workspace/{id}/members/{memberId}/suspend - Temporary de-activation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Fast mitigation to suspend access during security investigations without dropping historic auditing records.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Invokes <see cref="SuspendMemberCommand"/>, flips status flag in DB, and revokes active JWT claims cache.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals data security boundaries. The suspended status blocks all downstream workspace resource requests.</para>
    /// </remarks>
    /// <param name="id">Workspace identifier.</param>
    /// <param name="memberId">Membership record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The suspended membership details.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpPost("{id:guid}/members/{memberId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MemberDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SuspendMember(Guid id, Guid memberId, CancellationToken ct)
    {
        if (id != _currentUserContext.WorkspaceId)
        {
            return ForbidAccess();
        }

        var command = new SuspendMemberCommand(id, memberId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    private ActionResult ForbidAccess()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden Tenant Access",
            Detail = "Requested workspace does not match the active session workspace claim.",
            Instance = HttpContext.Request.Path
        });
    }
}

/// <summary>
/// Defines creation requirements for a new workspace.
/// </summary>
public record CreateWorkspaceRequest(string Name, string Slug);

/// <summary>
/// Defines parameters required to accept a pending invitation.
/// </summary>
public record AcceptInviteRequest(string Token);

/// <summary>
/// Defines parameters required to update a member's role.
/// </summary>
public record ChangeMemberRoleRequest(WorkspaceRole Role);
