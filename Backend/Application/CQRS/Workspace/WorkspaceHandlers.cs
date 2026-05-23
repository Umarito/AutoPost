using Application.Abstractions.Caching;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.DTOs.Workspace;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using WorkspaceEntity = Domain.Entities.Workspace;
using WorkspaceMemberEntity = Domain.Entities.WorkspaceMember;

namespace Application.CQRS.Workspace;

/// <summary>
/// Creates a new workspace for the current authenticated user and bootstraps the owner membership.
/// </summary>
public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, Result<WorkspaceDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreateWorkspaceCommandHandler> _logger;

    /// <summary>
    /// Initializes the workspace creation handler.
    /// </summary>
    public CreateWorkspaceCommandHandler(
        ICurrentUserContext currentUserContext,
        IApplicationUserRepository applicationUserRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<CreateWorkspaceCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _applicationUserRepository = applicationUserRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WorkspaceDto>> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _applicationUserRepository.GetByIdAsync(_currentUserContext.UserId, cancellationToken);
            if (user is null)
            {
                return Result<WorkspaceDto>.Fail("Current user was not found.", ErrorCode.Unauthorized);
            }

            var slug = await WorkspaceWorkflow.GenerateUniqueSlugAsync(
                WorkspaceWorkflow.NormalizeSlug(request.Slug),
                _workspaceRepository,
                cancellationToken);

            var utcNow = DateTime.UtcNow;
            var workspace = WorkspaceEntity.Create(request.Name, slug, utcNow);
            var ownerMembership = WorkspaceMemberEntity.CreateOwner(workspace.Id, user.Id, user.Email ?? string.Empty, utcNow);
            var defaults = NotificationPreference.CreateDefaults(user.Id, workspace.Id);

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            await _workspaceRepository.AddAsync(workspace, cancellationToken);
            await _workspaceMemberRepository.AddAsync(ownerMembership, cancellationToken);

            foreach (var preference in defaults)
            {
                await _notificationPreferenceRepository.AddAsync(preference, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, workspace.Id, cancellationToken);

            _logger.LogInformation(
                "Created workspace {WorkspaceId} for user {UserId}.",
                workspace.Id,
                user.Id);

            return Result<WorkspaceDto>.Ok(
                WorkspaceWorkflow.MapWorkspaceDto(
                    workspace,
                    memberCount: 1,
                    monthlyPostLimit: WorkspaceWorkflow.GetMonthlyPostLimit(workspace.Plan)));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while creating a workspace for user {UserId}.", _currentUserContext.UserId);
            return Result<WorkspaceDto>.Fail("An unexpected workspace creation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Loads one workspace while enforcing database-backed membership authorization and Redis read caching.
/// </summary>
public sealed class GetWorkspaceQueryHandler : IRequestHandler<GetWorkspaceQuery, Result<WorkspaceDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetWorkspaceQueryHandler> _logger;

    /// <summary>
    /// Initializes the workspace query handler.
    /// </summary>
    public GetWorkspaceQueryHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        ILogger<GetWorkspaceQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WorkspaceDto>> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = WorkspaceWorkflow.BuildWorkspaceCacheKey(request.WorkspaceId);

        try
        {
            var membership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: false,
                cancellationToken);

            if (!membership.IsSuccess)
            {
                return Result<WorkspaceDto>.Fail(membership.Error!, membership.Code!.Value);
            }

            var cached = await _cacheService.GetAsync<WorkspaceDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<WorkspaceDto>.Ok(cached);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result<WorkspaceDto>.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            var memberCount = await _workspaceMemberRepository.CountByWorkspaceAsync(
                workspace.Id,
                MemberStatus.Active,
                cancellationToken);

            var dto = WorkspaceWorkflow.MapWorkspaceDto(
                workspace,
                memberCount,
                WorkspaceWorkflow.GetMonthlyPostLimit(workspace.Plan));

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<WorkspaceDto>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading workspace {WorkspaceId}.", request.WorkspaceId);
            return Result<WorkspaceDto>.Fail("An unexpected workspace lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Updates workspace branding after verifying owner/admin permissions against the database.
/// </summary>
public sealed class UpdateWorkspaceCommandHandler : IRequestHandler<UpdateWorkspaceCommand, Result<WorkspaceDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UpdateWorkspaceCommandHandler> _logger;

    /// <summary>
    /// Initializes the workspace update handler.
    /// </summary>
    public UpdateWorkspaceCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<UpdateWorkspaceCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WorkspaceDto>> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var membership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!membership.IsSuccess)
            {
                return Result<WorkspaceDto>.Fail(membership.Error!, membership.Code!.Value);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result<WorkspaceDto>.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            workspace.UpdateBranding(request.Request.Name, request.Request.LogoUrl);
            _workspaceRepository.Update(workspace);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, workspace.Id, cancellationToken);

            var memberCount = await _workspaceMemberRepository.CountByWorkspaceAsync(
                workspace.Id,
                MemberStatus.Active,
                cancellationToken);

            _logger.LogInformation(
                "Updated workspace {WorkspaceId} by user {UserId}.",
                workspace.Id,
                _currentUserContext.UserId);

            return Result<WorkspaceDto>.Ok(
                WorkspaceWorkflow.MapWorkspaceDto(
                    workspace,
                    memberCount,
                    WorkspaceWorkflow.GetMonthlyPostLimit(workspace.Plan)));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while updating workspace {WorkspaceId}.", request.WorkspaceId);
            return Result<WorkspaceDto>.Fail("An unexpected workspace update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Soft-deactivates a workspace so it can no longer be used for operations.
/// </summary>
public sealed class DeactivateWorkspaceCommandHandler : IRequestHandler<DeactivateWorkspaceCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeactivateWorkspaceCommandHandler> _logger;

    /// <summary>
    /// Initializes the workspace deactivation handler.
    /// </summary>
    public DeactivateWorkspaceCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<DeactivateWorkspaceCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(DeactivateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var membership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!membership.IsSuccess)
            {
                return Result.Fail(membership.Error!, membership.Code!.Value);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            workspace.Deactivate();
            _workspaceRepository.Update(workspace);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, workspace.Id, cancellationToken);

            _logger.LogInformation(
                "Deactivated workspace {WorkspaceId} by user {UserId}.",
                workspace.Id,
                _currentUserContext.UserId);

            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while deactivating workspace {WorkspaceId}.", request.WorkspaceId);
            return Result.Fail("An unexpected workspace deactivation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Lists workspace members using server-side paging and strict RBAC checks.
/// </summary>
public sealed class GetWorkspaceMembersQueryHandler : IRequestHandler<GetWorkspaceMembersQuery, Result<PagedResult<MemberDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ILogger<GetWorkspaceMembersQueryHandler> _logger;

    /// <summary>
    /// Initializes the members-list query handler.
    /// </summary>
    public GetWorkspaceMembersQueryHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ILogger<GetWorkspaceMembersQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<MemberDto>>> Handle(GetWorkspaceMembersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var membership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!membership.IsSuccess)
            {
                return Result<PagedResult<MemberDto>>.Fail(membership.Error!, membership.Code!.Value);
            }

            var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;
            var total = await _workspaceMemberRepository.CountByWorkspaceAsync(request.WorkspaceId, status: null, cancellationToken);
            var items = await _workspaceMemberRepository.GetPagedByWorkspaceIdAsync(
                request.WorkspaceId,
                skip,
                request.Pagination.PageSize,
                cancellationToken);

            var dto = new PagedResult<MemberDto>(
                items.Select(item => WorkspaceWorkflow.MapMemberDto(item)).ToList(),
                total,
                request.Pagination.Page,
                request.Pagination.PageSize);

            return Result<PagedResult<MemberDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while listing members for workspace {WorkspaceId}.", request.WorkspaceId);
            return Result<PagedResult<MemberDto>>.Fail("An unexpected workspace members lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Invites a new member and queues the invitation email through Hangfire-backed SMTP delivery.
/// </summary>
public sealed class InviteMemberCommandHandler : IRequestHandler<InviteMemberCommand, Result<MemberDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IInviteTokenService _inviteTokenService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<InviteMemberCommandHandler> _logger;

    /// <summary>
    /// Initializes the invitation handler.
    /// </summary>
    public InviteMemberCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        UserManager<ApplicationUser> userManager,
        IInviteTokenService inviteTokenService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<InviteMemberCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _userManager = userManager;
        _inviteTokenService = inviteTokenService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Request.Email.Trim().ToLowerInvariant();

        try
        {
            var actingMembership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!actingMembership.IsSuccess)
            {
                return Result<MemberDto>.Fail(actingMembership.Error!, actingMembership.Code!.Value);
            }

            if (request.Request.Role == WorkspaceRole.Owner)
            {
                return Result<MemberDto>.Fail("Owner role cannot be granted through invitations.", ErrorCode.Validation);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result<MemberDto>.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            var activeMembers = await _workspaceMemberRepository.CountByWorkspaceAsync(
                request.WorkspaceId,
                MemberStatus.Active,
                cancellationToken);

            if (workspace.MaxTeamMembers != int.MaxValue && activeMembers >= workspace.MaxTeamMembers)
            {
                return Result<MemberDto>.Fail("Workspace team-member limit has been reached.", ErrorCode.Conflict);
            }

            var existingInvite = await _workspaceMemberRepository.GetByInvitedEmailAsync(
                request.WorkspaceId,
                normalizedEmail,
                cancellationToken);

            if (existingInvite is not null)
            {
                return Result<MemberDto>.Fail("A membership or invitation already exists for this email.", ErrorCode.Conflict);
            }

            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser is not null &&
                await _workspaceMemberRepository.ExistsAsync(existingUser.Id, request.WorkspaceId, cancellationToken))
            {
                return Result<MemberDto>.Fail("The user is already a member of this workspace.", ErrorCode.Conflict);
            }

            var invitation = WorkspaceMemberEntity.CreateInvitation(
                request.WorkspaceId,
                normalizedEmail,
                request.Request.Role,
                _currentUserContext.UserId,
                DateTime.UtcNow,
                existingUser?.Id);

            await _workspaceMemberRepository.AddAsync(invitation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var inviteToken = _inviteTokenService.Generate(
                request.WorkspaceId,
                normalizedEmail,
                request.Request.Role.ToString(),
                DateTime.UtcNow.AddHours(48));

            await _emailService.SendAsync(
                WorkspaceWorkflow.BuildInviteEmail(workspace, normalizedEmail, request.Request.Role, inviteToken),
                cancellationToken);

            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, workspace.Id, cancellationToken);

            _logger.LogInformation(
                "Created workspace invitation {MembershipId} for workspace {WorkspaceId} and email {Email}.",
                invitation.Id,
                request.WorkspaceId,
                normalizedEmail);

            return Result<MemberDto>.Ok(WorkspaceWorkflow.MapMemberDto(invitation));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while inviting {Email} into workspace {WorkspaceId}.", normalizedEmail, request.WorkspaceId);
            return Result<MemberDto>.Fail("An unexpected workspace invitation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Accepts a workspace invitation after validating the protected token and the current authenticated user.
/// </summary>
public sealed class AcceptInviteCommandHandler : IRequestHandler<AcceptInviteCommand, Result<MemberDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IInviteTokenService _inviteTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AcceptInviteCommandHandler> _logger;

    /// <summary>
    /// Initializes the invitation-acceptance handler.
    /// </summary>
    public AcceptInviteCommandHandler(
        ICurrentUserContext currentUserContext,
        IApplicationUserRepository applicationUserRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IInviteTokenService inviteTokenService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<AcceptInviteCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _applicationUserRepository = applicationUserRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _inviteTokenService = inviteTokenService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var payload = _inviteTokenService.Validate(request.Token);
            var user = await _applicationUserRepository.GetByIdAsync(_currentUserContext.UserId, cancellationToken);
            if (user is null)
            {
                return Result<MemberDto>.Fail("Current user was not found.", ErrorCode.Unauthorized);
            }

            // Security: the invite can only be accepted by the account that owns the invited email.
            if (!string.Equals(user.Email, payload.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Result<MemberDto>.Fail("This invitation is intended for a different email address.", ErrorCode.Forbidden);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(payload.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result<MemberDto>.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            if (!workspace.IsActive)
            {
                return Result<MemberDto>.Fail("Workspace is not active.", ErrorCode.Conflict);
            }

            if (await _workspaceMemberRepository.ExistsAsync(user.Id, payload.WorkspaceId, cancellationToken))
            {
                return Result<MemberDto>.Fail("You are already a member of this workspace.", ErrorCode.Conflict);
            }

            var invitation = await _workspaceMemberRepository.GetByInvitedEmailAsync(
                payload.WorkspaceId,
                payload.Email.Trim().ToLowerInvariant(),
                cancellationToken);

            if (invitation is null || invitation.Status != MemberStatus.Invited)
            {
                return Result<MemberDto>.Fail("Invitation was not found or is no longer pending.", ErrorCode.NotFound);
            }

            invitation.Accept(user.Id, DateTime.UtcNow);
            _workspaceMemberRepository.Update(invitation);

            var preferences = await _notificationPreferenceRepository.GetByUserAndWorkspaceAsync(
                user.Id,
                payload.WorkspaceId,
                cancellationToken);

            if (preferences.Count == 0)
            {
                foreach (var preference in NotificationPreference.CreateDefaults(user.Id, payload.WorkspaceId))
                {
                    await _notificationPreferenceRepository.AddAsync(preference, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, payload.WorkspaceId, cancellationToken);

            _logger.LogInformation(
                "Accepted workspace invitation {MembershipId} for user {UserId}.",
                invitation.Id,
                user.Id);

            return Result<MemberDto>.Ok(WorkspaceWorkflow.MapMemberDto(invitation, user));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Invitation acceptance failed because the token was invalid.");
            return Result<MemberDto>.Fail("Invitation token is invalid or expired.", ErrorCode.Validation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while accepting a workspace invitation.");
            return Result<MemberDto>.Fail("An unexpected invitation acceptance error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Changes the role of an existing member while protecting the owner role from accidental privilege loss.
/// </summary>
public sealed class ChangeMemberRoleCommandHandler : IRequestHandler<ChangeMemberRoleCommand, Result<MemberDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ChangeMemberRoleCommandHandler> _logger;

    /// <summary>
    /// Initializes the role-change handler.
    /// </summary>
    public ChangeMemberRoleCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<ChangeMemberRoleCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actingMembership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!actingMembership.IsSuccess)
            {
                return Result<MemberDto>.Fail(actingMembership.Error!, actingMembership.Code!.Value);
            }

            if (request.Role == WorkspaceRole.Owner)
            {
                return Result<MemberDto>.Fail("Owner role transfer is not supported in the current MVP batch.", ErrorCode.Validation);
            }

            var targetMembership = await _workspaceMemberRepository.GetByIdAsync(request.MemberId, cancellationToken);
            if (targetMembership is null || targetMembership.WorkspaceId != request.WorkspaceId)
            {
                return Result<MemberDto>.Fail("Workspace member was not found.", ErrorCode.NotFound);
            }

            // Security: owner memberships are protected from downgrade/removal by this MVP batch.
            if (targetMembership.Role == WorkspaceRole.Owner)
            {
                return Result<MemberDto>.Fail("Owner membership cannot be downgraded in this operation.", ErrorCode.Forbidden);
            }

            targetMembership.ChangeRole(request.Role);
            _workspaceMemberRepository.Update(targetMembership);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, request.WorkspaceId, cancellationToken);

            _logger.LogInformation(
                "Changed role of member {MemberId} in workspace {WorkspaceId} to {Role}.",
                request.MemberId,
                request.WorkspaceId,
                request.Role);

            return Result<MemberDto>.Ok(WorkspaceWorkflow.MapMemberDto(targetMembership));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while changing role for member {MemberId}.", request.MemberId);
            return Result<MemberDto>.Fail("An unexpected member role update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Removes a member from a workspace and invalidates all refresh-token sessions of the removed user when applicable.
/// </summary>
public sealed class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RemoveMemberCommandHandler> _logger;

    /// <summary>
    /// Initializes the member-removal handler.
    /// </summary>
    public RemoveMemberCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService,
        ILogger<RemoveMemberCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actingMembership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!actingMembership.IsSuccess)
            {
                return Result.Fail(actingMembership.Error!, actingMembership.Code!.Value);
            }

            var targetMembership = await _workspaceMemberRepository.GetByIdAsync(request.MemberId, cancellationToken);
            if (targetMembership is null || targetMembership.WorkspaceId != request.WorkspaceId)
            {
                return Result.Fail("Workspace member was not found.", ErrorCode.NotFound);
            }

            if (targetMembership.Role == WorkspaceRole.Owner)
            {
                return Result.Fail("Owner membership cannot be removed in this operation.", ErrorCode.Forbidden);
            }

            if (targetMembership.UserId.HasValue)
            {
                await _refreshTokenRepository.RevokeAllForUserAsync(
                    targetMembership.UserId.Value,
                    DateTime.UtcNow,
                    cancellationToken);

                await WorkspaceWorkflow.InvalidateUserSessionCachesAsync(
                    _cacheService,
                    targetMembership.UserId.Value,
                    cancellationToken);
            }

            _workspaceMemberRepository.Remove(targetMembership);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, request.WorkspaceId, cancellationToken);

            if (targetMembership.User?.Email is not null)
            {
                await _emailService.SendAsync(
                    WorkspaceWorkflow.BuildMembershipStatusEmail(
                        targetMembership.User.Email,
                        "You were removed from an AutoPost workspace",
                        "Your access to the workspace has been removed."),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Removed member {MemberId} from workspace {WorkspaceId}.",
                request.MemberId,
                request.WorkspaceId);

            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while removing member {MemberId}.", request.MemberId);
            return Result.Fail("An unexpected member removal error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Suspends a member without deleting the membership record and invalidates their sessions for security.
/// </summary>
public sealed class SuspendMemberCommandHandler : IRequestHandler<SuspendMemberCommand, Result<MemberDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILogger<SuspendMemberCommandHandler> _logger;

    /// <summary>
    /// Initializes the member-suspension handler.
    /// </summary>
    public SuspendMemberCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService,
        ILogger<SuspendMemberCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MemberDto>> Handle(SuspendMemberCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actingMembership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: true,
                cancellationToken);

            if (!actingMembership.IsSuccess)
            {
                return Result<MemberDto>.Fail(actingMembership.Error!, actingMembership.Code!.Value);
            }

            var targetMembership = await _workspaceMemberRepository.GetByIdAsync(request.MemberId, cancellationToken);
            if (targetMembership is null || targetMembership.WorkspaceId != request.WorkspaceId)
            {
                return Result<MemberDto>.Fail("Workspace member was not found.", ErrorCode.NotFound);
            }

            if (targetMembership.Role == WorkspaceRole.Owner)
            {
                return Result<MemberDto>.Fail("Owner membership cannot be suspended.", ErrorCode.Forbidden);
            }

            targetMembership.Suspend();
            _workspaceMemberRepository.Update(targetMembership);

            if (targetMembership.UserId.HasValue)
            {
                await _refreshTokenRepository.RevokeAllForUserAsync(
                    targetMembership.UserId.Value,
                    DateTime.UtcNow,
                    cancellationToken);

                await WorkspaceWorkflow.InvalidateUserSessionCachesAsync(
                    _cacheService,
                    targetMembership.UserId.Value,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await WorkspaceWorkflow.InvalidateWorkspaceCachesAsync(_cacheService, request.WorkspaceId, cancellationToken);

            if (targetMembership.User?.Email is not null)
            {
                await _emailService.SendAsync(
                    WorkspaceWorkflow.BuildMembershipStatusEmail(
                        targetMembership.User.Email,
                        "Your AutoPost workspace access was suspended",
                        "Your workspace membership has been suspended by an administrator."),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Suspended member {MemberId} in workspace {WorkspaceId}.",
                request.MemberId,
                request.WorkspaceId);

            return Result<MemberDto>.Ok(WorkspaceWorkflow.MapMemberDto(targetMembership));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while suspending member {MemberId}.", request.MemberId);
            return Result<MemberDto>.Fail("An unexpected member suspension error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Aggregates current workspace plan usage for read-heavy billing and settings screens and caches the projection in Redis.
/// </summary>
public sealed class GetWorkspacePlanUsageQueryHandler : IRequestHandler<GetWorkspacePlanUsageQuery, Result<WorkspacePlanUsageDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetWorkspacePlanUsageQueryHandler> _logger;

    /// <summary>
    /// Initializes the plan-usage query handler.
    /// </summary>
    public GetWorkspacePlanUsageQueryHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ISocialAccountRepository socialAccountRepository,
        IPostRepository postRepository,
        ICacheService cacheService,
        ILogger<GetWorkspacePlanUsageQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _socialAccountRepository = socialAccountRepository;
        _postRepository = postRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WorkspacePlanUsageDto>> Handle(GetWorkspacePlanUsageQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = WorkspaceWorkflow.BuildWorkspaceUsageCacheKey(request.WorkspaceId);

        try
        {
            var membership = await WorkspaceWorkflow.LoadAuthorizedMembershipAsync(
                _currentUserContext.UserId,
                request.WorkspaceId,
                _workspaceMemberRepository,
                requireManagementRole: false,
                cancellationToken);

            if (!membership.IsSuccess)
            {
                return Result<WorkspacePlanUsageDto>.Fail(membership.Error!, membership.Code!.Value);
            }

            var cached = await _cacheService.GetAsync<WorkspacePlanUsageDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<WorkspacePlanUsageDto>.Ok(cached);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return Result<WorkspacePlanUsageDto>.Fail("Workspace was not found.", ErrorCode.NotFound);
            }

            var membersUsed = await _workspaceMemberRepository.CountByWorkspaceAsync(
                request.WorkspaceId,
                MemberStatus.Active,
                cancellationToken);

            var socialAccountsUsed = await _socialAccountRepository.CountActiveByWorkspaceIdAsync(
                request.WorkspaceId,
                cancellationToken);

            var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonthStartUtc = monthStartUtc.AddMonths(1);

            var postsUsed = await _postRepository.CountByWorkspaceIdForPeriodAsync(
                request.WorkspaceId,
                monthStartUtc,
                nextMonthStartUtc,
                cancellationToken);

            var dto = new WorkspacePlanUsageDto(
                workspace.Id,
                workspace.Plan.ToString(),
                socialAccountsUsed,
                workspace.MaxSocialAccounts,
                membersUsed,
                workspace.MaxTeamMembers,
                postsUsed,
                WorkspaceWorkflow.GetMonthlyPostLimit(workspace.Plan));

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<WorkspacePlanUsageDto>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading plan usage for workspace {WorkspaceId}.", request.WorkspaceId);
            return Result<WorkspacePlanUsageDto>.Fail("An unexpected workspace usage lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Contains shared workspace authorization, mapping and cache helpers used by the first MVP batch.
/// </summary>
internal static class WorkspaceWorkflow
{
    /// <summary>
    /// Normalizes a user-supplied slug candidate into the URL-safe format used by workspace routing.
    /// </summary>
    public static string NormalizeSlug(string slug)
    {
        var cleaned = new string(slug
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cleaned.Trim('-')) ? "workspace" : cleaned.Trim('-');
    }

    /// <summary>
    /// Generates a unique slug by suffixing numeric values until the uniqueness constraint is satisfied.
    /// </summary>
    public static async Task<string> GenerateUniqueSlugAsync(
        string baseSlug,
        IWorkspaceRepository workspaceRepository,
        CancellationToken cancellationToken)
    {
        var safeBaseSlug = string.IsNullOrWhiteSpace(baseSlug) ? "workspace" : baseSlug;
        var candidate = safeBaseSlug;
        var suffix = 2;

        while (await workspaceRepository.SlugExistsAsync(candidate, cancellationToken))
        {
            candidate = $"{safeBaseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>
    /// Loads and authorizes the current user's membership for the target workspace directly from the database.
    /// </summary>
    public static async Task<Result<WorkspaceMember>> LoadAuthorizedMembershipAsync(
        Guid currentUserId,
        Guid workspaceId,
        IWorkspaceMemberRepository workspaceMemberRepository,
        bool requireManagementRole,
        CancellationToken cancellationToken)
    {
        var membership = await workspaceMemberRepository.GetByUserAndWorkspaceAsync(currentUserId, workspaceId, cancellationToken);
        if (membership is null || membership.Status != MemberStatus.Active)
        {
            return Result<WorkspaceMember>.Fail("Access to the requested workspace is forbidden.", ErrorCode.Forbidden);
        }

        if (requireManagementRole && membership.Role is not (WorkspaceRole.Owner or WorkspaceRole.Admin))
        {
            return Result<WorkspaceMember>.Fail("Workspace management permissions are required.", ErrorCode.Forbidden);
        }

        return Result<WorkspaceMember>.Ok(membership);
    }

    /// <summary>
    /// Builds the Redis cache key for one workspace projection.
    /// </summary>
    public static string BuildWorkspaceCacheKey(Guid workspaceId)
    {
        return $"workspace:{workspaceId}";
    }

    /// <summary>
    /// Builds the Redis cache key for one workspace usage projection.
    /// </summary>
    public static string BuildWorkspaceUsageCacheKey(Guid workspaceId)
    {
        return $"workspace:{workspaceId}:usage";
    }

    /// <summary>
    /// Invalidates workspace projections affected by write-side commands.
    /// </summary>
    public static async Task InvalidateWorkspaceCachesAsync(ICacheService cacheService, Guid workspaceId, CancellationToken cancellationToken)
    {
        await cacheService.RemoveAsync(BuildWorkspaceCacheKey(workspaceId), cancellationToken);
        await cacheService.RemoveAsync(BuildWorkspaceUsageCacheKey(workspaceId), cancellationToken);
    }

    /// <summary>
    /// Invalidates user-session cache projections affected by workspace membership security changes.
    /// </summary>
    public static async Task InvalidateUserSessionCachesAsync(ICacheService cacheService, Guid userId, CancellationToken cancellationToken)
    {
        await cacheService.RemoveAsync($"auth:sessions:all:{userId}", cancellationToken);
        await cacheService.RemoveAsync($"auth:sessions:active:{userId}", cancellationToken);
    }

    /// <summary>
    /// Maps a workspace aggregate to the API DTO used by workspace settings and selector screens.
    /// </summary>
    public static WorkspaceDto MapWorkspaceDto(WorkspaceEntity workspace, int memberCount, int monthlyPostLimit)
    {
        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Slug,
            workspace.LogoUrl,
            workspace.Plan.ToString(),
            workspace.MaxSocialAccounts,
            workspace.MaxTeamMembers,
            monthlyPostLimit,
            workspace.IsActive,
            memberCount,
            workspace.CreatedAt);
    }

    /// <summary>
    /// Maps a membership to the API DTO while safely handling pending invitations without a bound user.
    /// </summary>
    public static MemberDto MapMemberDto(WorkspaceMemberEntity membership, ApplicationUser? userOverride = null)
    {
        var user = userOverride ?? membership.User;

        return new MemberDto(
            membership.Id,
            membership.UserId,
            user?.DisplayName ?? membership.InvitedEmail,
            user?.Email ?? membership.InvitedEmail,
            user?.AvatarUrl,
            membership.Role.ToString(),
            membership.Status.ToString(),
            membership.JoinedAt);
    }

    /// <summary>
    /// Returns the monthly post limit exposed to the current MVP API surface.
    /// </summary>
    public static int GetMonthlyPostLimit(SubscriptionPlan plan)
    {
        // The imported TRD enumerates member/social-account quotas explicitly, but does not define a final monthly-post catalog.
        // The API currently surfaces 0 to represent "not configured in the current billing catalog yet".
        return 0;
    }

    /// <summary>
    /// Builds the transactional invitation email payload.
    /// </summary>
    public static EmailMessage BuildInviteEmail(WorkspaceEntity workspace, string email, WorkspaceRole role, string inviteToken)
    {
        var subject = $"Invitation to join {workspace.Name} on AutoPost";
        var htmlBody =
            $"<p>You have been invited to join <strong>{System.Net.WebUtility.HtmlEncode(workspace.Name)}</strong> on AutoPost as <strong>{role}</strong>.</p>" +
            "<p>Use the following invitation token to complete the join flow:</p>" +
            $"<pre>{System.Net.WebUtility.HtmlEncode(inviteToken)}</pre>";

        var textBody =
            $"You were invited to join {workspace.Name} on AutoPost as {role}.{Environment.NewLine}" +
            $"Invitation token:{Environment.NewLine}{inviteToken}";

        return new EmailMessage(email, subject, htmlBody, textBody);
    }

    /// <summary>
    /// Builds a simple transactional email for removal/suspension notifications.
    /// </summary>
    public static EmailMessage BuildMembershipStatusEmail(string email, string subject, string message)
    {
        return new EmailMessage(
            email,
            subject,
            $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
            message);
    }
}
