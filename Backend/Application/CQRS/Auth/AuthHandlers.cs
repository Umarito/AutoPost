using System.Security.Claims;
using System.Security.Cryptography;
using Application.Abstractions.Caching;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.RateLimiting;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.Common;
using Application.DTOs.Auth;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using WorkspaceEntity = Domain.Entities.Workspace;
using WorkspaceMemberEntity = Domain.Entities.WorkspaceMember;

namespace Application.CQRS.Auth;

/// <summary>
/// Handles user registration, secure initial workspace bootstrap and immediate session issuance.
/// </summary>
public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthTokensDto>>
{
    private const int RegisterPermitLimit = 5;
    private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(15);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisRateLimitService _rateLimitService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    /// <summary>
    /// Initializes the registration handler.
    /// </summary>
    public RegisterUserCommandHandler(
        UserManager<ApplicationUser> userManager,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        IRedisRateLimitService rateLimitService,
        IEmailService emailService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userManager = userManager;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _rateLimitService = rateLimitService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthWorkflow.NormalizeEmail(request.Request.Email);

        try
        {
            // Security: a distributed Redis rate limit protects registration against account-creation floods.
            var decision = await _rateLimitService.ConsumeAsync(
                "register-email",
                normalizedEmail,
                RegisterPermitLimit,
                RegisterWindow,
                cancellationToken);

            if (!decision.IsAllowed)
            {
                return Result<AuthTokensDto>.Fail(
                    "Too many registration attempts. Please try again later.",
                    ErrorCode.Validation);
            }

            if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
            {
                return Result<AuthTokensDto>.Fail("Email address is already registered.", ErrorCode.Conflict);
            }

            var utcNow = DateTime.UtcNow;
            var user = ApplicationUser.Create(normalizedEmail, request.Request.DisplayName, utcNow);
            var workspaceName = $"{user.DisplayName}'s Workspace";
            var baseSlug = AuthWorkflow.Slugify(user.DisplayName);
            var slug = await AuthWorkflow.GenerateUniqueSlugAsync(baseSlug, _workspaceRepository, cancellationToken);
            var workspace = WorkspaceEntity.Create(workspaceName, slug, utcNow);
            var ownerMembership = WorkspaceMemberEntity.CreateOwner(workspace.Id, user.Id, normalizedEmail, utcNow);
            var defaults = NotificationPreference.CreateDefaults(user.Id, workspace.Id);

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var identityResult = await _userManager.CreateAsync(user, request.Request.Password);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<AuthTokensDto>.Fail(
                    string.Join("; ", identityResult.Errors.Select(error => error.Description)),
                    ErrorCode.Validation);
            }

            await _workspaceRepository.AddAsync(workspace, cancellationToken);
            await _workspaceMemberRepository.AddAsync(ownerMembership, cancellationToken);

            foreach (var preference in defaults)
            {
                await _notificationPreferenceRepository.AddAsync(preference, cancellationToken);
            }

            var tokens = await AuthWorkflow.IssueTokensAsync(
                user,
                ownerMembership,
                _refreshTokenRepository,
                _refreshTokenHasher,
                _jwtTokenGenerator,
                utcNow,
                deviceInfo: null,
                ipAddress: null,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _emailService.SendAsync(
                AuthWorkflow.BuildConfirmationEmail(user, confirmationToken),
                cancellationToken);

            _logger.LogInformation(
                "Registered user {UserId} and bootstrap workspace {WorkspaceId}.",
                user.Id,
                workspace.Id);

            return Result<AuthTokensDto>.Ok(tokens);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while registering user {Email}.", normalizedEmail);
            return Result<AuthTokensDto>.Fail("An unexpected registration error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Handles credential-based login, Redis-backed brute-force protection and secure token issuance.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private const int LoginPermitLimit = 5;
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRedisRateLimitService _rateLimitService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Initializes the login handler.
    /// </summary>
    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        IRedisRateLimitService rateLimitService,
        ICacheService cacheService,
        ILogger<LoginCommandHandler> logger)
    {
        _userManager = userManager;
        _workspaceMemberRepository = workspaceMemberRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _rateLimitService = rateLimitService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthWorkflow.NormalizeEmail(request.Request.Email);

        try
        {
            var decision = await _rateLimitService.ConsumeAsync(
                "login-email",
                normalizedEmail,
                LoginPermitLimit,
                LoginWindow,
                cancellationToken);

            if (!decision.IsAllowed)
            {
                return Result<AuthTokensDto>.Fail(
                    "Too many login attempts. Please wait before trying again.",
                    ErrorCode.Validation);
            }

            var user = await _userManager.FindByEmailAsync(normalizedEmail);

            // Security: the response intentionally does not reveal whether the email or password was incorrect.
            if (user is null || !user.IsActive)
            {
                return Result<AuthTokensDto>.Fail("Invalid credentials.", ErrorCode.Unauthorized);
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Request.Password);
            if (!passwordValid)
            {
                return Result<AuthTokensDto>.Fail("Invalid credentials.", ErrorCode.Unauthorized);
            }

            var membership = await AuthWorkflow.ResolvePrimaryMembershipAsync(
                user.Id,
                _workspaceMemberRepository,
                cancellationToken);

            if (membership is null)
            {
                return Result<AuthTokensDto>.Fail(
                    "No active workspace membership is available for this account.",
                    ErrorCode.Forbidden);
            }

            var utcNow = DateTime.UtcNow;
            user.MarkSuccessfulLogin(utcNow);

            var tokens = await AuthWorkflow.IssueTokensAsync(
                user,
                membership,
                _refreshTokenRepository,
                _refreshTokenHasher,
                _jwtTokenGenerator,
                utcNow,
                deviceInfo: null,
                ipAddress: null,
                cancellationToken);

            await _rateLimitService.ResetAsync("login-email", normalizedEmail, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AuthWorkflow.InvalidateSessionCachesAsync(_cacheService, user.Id, cancellationToken);

            _logger.LogInformation(
                "Logged in user {UserId} for workspace {WorkspaceId}.",
                user.Id,
                membership.WorkspaceId);

            return Result<AuthTokensDto>.Ok(tokens);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while logging in {Email}.", normalizedEmail);
            return Result<AuthTokensDto>.Fail("An unexpected login error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Handles refresh-token rotation and theft detection.
/// </summary>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    /// <summary>
    /// Initializes the refresh-token handler.
    /// </summary>
    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
        _workspaceMemberRepository = workspaceMemberRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return AuthWorkflow.RotateRefreshTokenAsync(
            request.RefreshToken,
            _refreshTokenRepository,
            _refreshTokenHasher,
            _workspaceMemberRepository,
            _jwtTokenGenerator,
            _unitOfWork,
            _cacheService,
            _logger,
            cancellationToken);
    }
}

/// <summary>
/// Revokes the current session represented by one concrete refresh token.
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    /// <summary>
    /// Initializes the logout handler.
    /// </summary>
    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<LogoutCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tokenHash = _refreshTokenHasher.Hash(request.RefreshToken);
            var token = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            // Security: logout is intentionally idempotent to avoid leaking session existence.
            if (token is null || token.IsRevoked)
            {
                return Result.Ok();
            }

            token.Revoke(DateTime.UtcNow);
            _refreshTokenRepository.Update(token);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AuthWorkflow.InvalidateSessionCachesAsync(_cacheService, token.UserId, cancellationToken);

            _logger.LogInformation("Revoked refresh token session {RefreshTokenId}.", token.Id);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while revoking the current session.");
            return Result.Fail("An unexpected logout error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Revokes every active refresh token of the current authenticated user.
/// </summary>
public sealed class LogoutAllDevicesCommandHandler : IRequestHandler<LogoutAllDevicesCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LogoutAllDevicesCommandHandler> _logger;

    /// <summary>
    /// Initializes the logout-all-devices handler.
    /// </summary>
    public LogoutAllDevicesCommandHandler(
        ICurrentUserContext currentUserContext,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<LogoutAllDevicesCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(LogoutAllDevicesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(
                _currentUserContext.UserId,
                DateTime.UtcNow,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AuthWorkflow.InvalidateSessionCachesAsync(_cacheService, _currentUserContext.UserId, cancellationToken);

            _logger.LogInformation("Revoked all sessions for user {UserId}.", _currentUserContext.UserId);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while revoking all sessions for user {UserId}.", _currentUserContext.UserId);
            return Result.Fail("An unexpected logout-all-devices error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Confirms email ownership through ASP.NET Core Identity.
/// </summary>
public sealed class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ConfirmEmailCommandHandler> _logger;

    /// <summary>
    /// Initializes the email-confirmation handler.
    /// </summary>
    public ConfirmEmailCommandHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<ConfirmEmailCommandHandler> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null)
            {
                return Result.Fail("User account was not found.", ErrorCode.NotFound);
            }

            if (user.EmailConfirmed)
            {
                return Result.Ok();
            }

            var result = await _userManager.ConfirmEmailAsync(user, request.Token);
            if (!result.Succeeded)
            {
                return Result.Fail(
                    string.Join("; ", result.Errors.Select(error => error.Description)),
                    ErrorCode.Validation);
            }

            _logger.LogInformation("Confirmed email for user {UserId}.", user.Id);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while confirming email for user {UserId}.", request.UserId);
            return Result.Fail("An unexpected email confirmation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Re-sends an email confirmation message while applying Redis-backed anti-spam protection.
/// </summary>
public sealed class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand, Result>
{
    private const int ResendPermitLimit = 3;
    private static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRedisRateLimitService _rateLimitService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendEmailConfirmationCommandHandler> _logger;

    /// <summary>
    /// Initializes the resend-confirmation handler.
    /// </summary>
    public ResendEmailConfirmationCommandHandler(
        UserManager<ApplicationUser> userManager,
        IRedisRateLimitService rateLimitService,
        IEmailService emailService,
        ILogger<ResendEmailConfirmationCommandHandler> logger)
    {
        _userManager = userManager;
        _rateLimitService = rateLimitService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthWorkflow.NormalizeEmail(request.Email);

        try
        {
            var decision = await _rateLimitService.ConsumeAsync(
                "resend-confirmation-email",
                normalizedEmail,
                ResendPermitLimit,
                ResendWindow,
                cancellationToken);

            if (!decision.IsAllowed)
            {
                return Result.Fail(
                    "Too many confirmation email requests. Please try again later.",
                    ErrorCode.Validation);
            }

            var user = await _userManager.FindByEmailAsync(normalizedEmail);

            // Security: the response intentionally does not disclose whether the email is registered.
            if (user is null || user.EmailConfirmed)
            {
                return Result.Ok();
            }

            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _emailService.SendAsync(
                AuthWorkflow.BuildConfirmationEmail(user, confirmationToken),
                cancellationToken);

            _logger.LogInformation("Queued another email confirmation message for user {UserId}.", user.Id);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while re-sending confirmation for {Email}.", normalizedEmail);
            return Result.Fail("An unexpected resend-confirmation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Updates the editable profile of the current authenticated user.
/// </summary>
public sealed class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateUserProfileCommandHandler> _logger;

    /// <summary>
    /// Initializes the profile-update handler.
    /// </summary>
    public UpdateUserProfileCommandHandler(
        ICurrentUserContext currentUserContext,
        IApplicationUserRepository applicationUserRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateUserProfileCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _applicationUserRepository = applicationUserRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _applicationUserRepository.GetByIdAsync(_currentUserContext.UserId, cancellationToken);
            if (user is null)
            {
                return Result<UserProfileDto>.Fail("Current user account was not found.", ErrorCode.Unauthorized);
            }

            user.UpdateProfile(
                request.Request.DisplayName,
                request.Request.AvatarUrl,
                request.Request.TimeZoneId,
                request.Request.Locale);

            _applicationUserRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated profile for user {UserId}.", user.Id);
            return Result<UserProfileDto>.Ok(AuthWorkflow.MapUserProfile(user));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while updating profile for user {UserId}.", _currentUserContext.UserId);
            return Result<UserProfileDto>.Fail("An unexpected profile update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Retrieves the safe public profile of the current user.
/// </summary>
public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserProfileDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly ILogger<GetUserByIdQueryHandler> _logger;

    /// <summary>
    /// Initializes the user-profile query handler.
    /// </summary>
    public GetUserByIdQueryHandler(
        ICurrentUserContext currentUserContext,
        IApplicationUserRepository applicationUserRepository,
        ILogger<GetUserByIdQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _applicationUserRepository = applicationUserRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Security: self-service profile lookup blocks IDOR by denying access to foreign user identifiers.
            if (request.UserId != _currentUserContext.UserId)
            {
                return Result<UserProfileDto>.Fail("Access to another user's profile is forbidden.", ErrorCode.Forbidden);
            }

            var user = await _applicationUserRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                return Result<UserProfileDto>.Fail("User account was not found.", ErrorCode.NotFound);
            }

            return Result<UserProfileDto>.Ok(AuthWorkflow.MapUserProfile(user));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading profile for user {UserId}.", request.UserId);
            return Result<UserProfileDto>.Fail("An unexpected profile lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Lists all sessions of the current authenticated user and caches the projection in Redis.
/// </summary>
public sealed class GetUserSessionsQueryHandler : IRequestHandler<GetUserSessionsQuery, Result<IReadOnlyList<UserSessionDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetUserSessionsQueryHandler> _logger;

    /// <summary>
    /// Initializes the sessions query handler.
    /// </summary>
    public GetUserSessionsQueryHandler(
        ICurrentUserContext currentUserContext,
        IRefreshTokenRepository refreshTokenRepository,
        ICacheService cacheService,
        ILogger<GetUserSessionsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _refreshTokenRepository = refreshTokenRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UserSessionDto>>> Handle(GetUserSessionsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = AuthWorkflow.BuildSessionsCacheKey(_currentUserContext.UserId, activeOnly: false);

        try
        {
            var cached = await _cacheService.GetAsync<List<UserSessionDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<UserSessionDto>>.Ok(cached);
            }

            var sessions = await _refreshTokenRepository.GetByUserIdAsync(_currentUserContext.UserId, activeOnly: false, cancellationToken);
            var result = sessions
                .Select(session => AuthWorkflow.MapSession(session, _currentUserContext.SessionId))
                .ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
            return Result<IReadOnlyList<UserSessionDto>>.Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading sessions for user {UserId}.", _currentUserContext.UserId);
            return Result<IReadOnlyList<UserSessionDto>>.Fail("An unexpected sessions lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Lists only active sessions of the current authenticated user and caches the projection in Redis.
/// </summary>
public sealed class GetActiveSessionsQueryHandler : IRequestHandler<GetActiveSessionsQuery, Result<IReadOnlyList<UserSessionDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetActiveSessionsQueryHandler> _logger;

    /// <summary>
    /// Initializes the active-sessions query handler.
    /// </summary>
    public GetActiveSessionsQueryHandler(
        ICurrentUserContext currentUserContext,
        IRefreshTokenRepository refreshTokenRepository,
        ICacheService cacheService,
        ILogger<GetActiveSessionsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _refreshTokenRepository = refreshTokenRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UserSessionDto>>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = AuthWorkflow.BuildSessionsCacheKey(_currentUserContext.UserId, activeOnly: true);

        try
        {
            var cached = await _cacheService.GetAsync<List<UserSessionDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<UserSessionDto>>.Ok(cached);
            }

            var sessions = await _refreshTokenRepository.GetByUserIdAsync(_currentUserContext.UserId, activeOnly: true, cancellationToken);
            var result = sessions
                .Select(session => AuthWorkflow.MapSession(session, _currentUserContext.SessionId))
                .ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
            return Result<IReadOnlyList<UserSessionDto>>.Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading active sessions for user {UserId}.", _currentUserContext.UserId);
            return Result<IReadOnlyList<UserSessionDto>>.Fail("An unexpected active sessions lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Performs explicit refresh-token rotation through the same secure workflow used by the normal refresh endpoint.
/// </summary>
public sealed class RotateRefreshTokenCommandHandler : IRequestHandler<RotateRefreshTokenCommand, Result<AuthTokensDto>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RotateRefreshTokenCommandHandler> _logger;

    /// <summary>
    /// Initializes the explicit rotation handler.
    /// </summary>
    public RotateRefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<RotateRefreshTokenCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
        _workspaceMemberRepository = workspaceMemberRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<AuthTokensDto>> Handle(RotateRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return AuthWorkflow.RotateRefreshTokenAsync(
            request.RefreshToken,
            _refreshTokenRepository,
            _refreshTokenHasher,
            _workspaceMemberRepository,
            _jwtTokenGenerator,
            _unitOfWork,
            _cacheService,
            _logger,
            cancellationToken);
    }
}

/// <summary>
/// Revokes one specific session while enforcing ownership through the current JWT subject.
/// </summary>
public sealed class RevokeRefreshTokenCommandHandler : IRequestHandler<RevokeRefreshTokenCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RevokeRefreshTokenCommandHandler> _logger;

    /// <summary>
    /// Initializes the session-revocation handler.
    /// </summary>
    public RevokeRefreshTokenCommandHandler(
        ICurrentUserContext currentUserContext,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<RevokeRefreshTokenCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _refreshTokenRepository.GetByIdAsync(request.RefreshTokenId, cancellationToken);
            if (token is null || token.UserId != _currentUserContext.UserId)
            {
                return Result.Fail("The requested session was not found.", ErrorCode.NotFound);
            }

            token.Revoke(DateTime.UtcNow);
            _refreshTokenRepository.Update(token);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await AuthWorkflow.InvalidateSessionCachesAsync(_cacheService, _currentUserContext.UserId, cancellationToken);

            _logger.LogInformation(
                "Revoked specific session {RefreshTokenId} for user {UserId}.",
                token.Id,
                _currentUserContext.UserId);

            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected failure while revoking session {RefreshTokenId} for user {UserId}.",
                request.RefreshTokenId,
                _currentUserContext.UserId);
            return Result.Fail("An unexpected session revocation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Cleans up expired refresh-token records so the session table remains compact and efficient.
/// </summary>
public sealed class CleanupExpiredTokensCommandHandler : IRequestHandler<CleanupExpiredTokensCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<CleanupExpiredTokensCommandHandler> _logger;

    /// <summary>
    /// Initializes the cleanup handler.
    /// </summary>
    public CleanupExpiredTokensCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<CleanupExpiredTokensCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(CleanupExpiredTokensCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _refreshTokenRepository.CleanupExpiredAsync(cancellationToken);
            _logger.LogInformation("Cleaned up expired refresh tokens.");
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while cleaning up expired refresh tokens.");
            return Result.Fail("An unexpected refresh-token cleanup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Contains shared authentication workflow helpers used by multiple handlers in this batch.
/// </summary>
internal static class AuthWorkflow
{
    private static readonly TimeSpan SessionCacheTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Normalizes email addresses for identity and rate-limit lookups.
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Converts a human-readable name into a URL-safe slug candidate.
    /// </summary>
    public static string Slugify(string value)
    {
        var cleaned = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return cleaned.Trim('-');
    }

    /// <summary>
    /// Generates a unique workspace slug by appending an incrementing numeric suffix when necessary.
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
    /// Selects the primary active membership used to issue the current JWT workspace claims.
    /// </summary>
    public static async Task<WorkspaceMember?> ResolvePrimaryMembershipAsync(
        Guid userId,
        IWorkspaceMemberRepository workspaceMemberRepository,
        CancellationToken cancellationToken)
    {
        var memberships = await workspaceMemberRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        return memberships.FirstOrDefault();
    }

    /// <summary>
    /// Creates a signed access token and a rotated refresh token session record.
    /// </summary>
    public static async Task<AuthTokensDto> IssueTokensAsync(
        ApplicationUser user,
        WorkspaceMember membership,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        DateTime utcNow,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = GenerateRawRefreshToken();
        var refreshTokenHash = refreshTokenHasher.Hash(rawRefreshToken);
        var refreshTokenExpiry = utcNow.AddDays(90);

        var refreshToken = RefreshToken.Issue(
            user.Id,
            refreshTokenHash,
            utcNow,
            refreshTokenExpiry,
            deviceInfo,
            ipAddress);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        var additionalClaims = new List<Claim>
        {
            new("sid", refreshToken.Id.ToString()),
            new("email_confirmed", user.EmailConfirmed ? "true" : "false")
        };

        var accessToken = jwtTokenGenerator.GenerateAccessToken(
            user,
            membership.WorkspaceId,
            membership.Role.ToString(),
            additionalClaims);

        return new AuthTokensDto(
            accessToken,
            rawRefreshToken,
            jwtTokenGenerator.GetAccessTokenExpiresAtUtc());
    }

    /// <summary>
    /// Rotates an incoming refresh token while detecting replay attacks.
    /// </summary>
    public static async Task<Result<AuthTokensDto>> RotateRefreshTokenAsync(
        string rawRefreshToken,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenHash = refreshTokenHasher.Hash(rawRefreshToken);
            var refreshToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
            if (refreshToken is null)
            {
                return Result<AuthTokensDto>.Fail("Refresh token is invalid.", ErrorCode.Unauthorized);
            }

            if (refreshToken.IsUsed)
            {
                // Security: a reused refresh token is treated as theft and all sessions are revoked immediately.
                await refreshTokenRepository.RevokeAllForUserAsync(refreshToken.UserId, DateTime.UtcNow, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await InvalidateSessionCachesAsync(cacheService, refreshToken.UserId, cancellationToken);

                logger.LogWarning(
                    "Detected refresh token replay for user {UserId}. All sessions were revoked.",
                    refreshToken.UserId);

                return Result<AuthTokensDto>.Fail("Refresh token is no longer valid.", ErrorCode.Unauthorized);
            }

            if (!refreshToken.IsActive(DateTime.UtcNow))
            {
                return Result<AuthTokensDto>.Fail("Refresh token has expired or was revoked.", ErrorCode.Unauthorized);
            }

            var membership = await ResolvePrimaryMembershipAsync(
                refreshToken.UserId,
                workspaceMemberRepository,
                cancellationToken);

            if (membership is null)
            {
                return Result<AuthTokensDto>.Fail(
                    "No active workspace membership is available for this account.",
                    ErrorCode.Forbidden);
            }

            refreshToken.MarkUsed();
            refreshTokenRepository.Update(refreshToken);

            var rotatedTokens = await IssueTokensAsync(
                refreshToken.User,
                membership,
                refreshTokenRepository,
                refreshTokenHasher,
                jwtTokenGenerator,
                DateTime.UtcNow,
                refreshToken.DeviceInfo,
                refreshToken.IpAddress,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await InvalidateSessionCachesAsync(cacheService, refreshToken.UserId, cancellationToken);

            return Result<AuthTokensDto>.Ok(rotatedTokens);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected failure while rotating refresh token.");
            return Result<AuthTokensDto>.Fail("An unexpected refresh-token rotation error occurred.", ErrorCode.Unknown);
        }
    }

    /// <summary>
    /// Creates the transactional confirmation email payload queued after registration or resend.
    /// </summary>
    public static EmailMessage BuildConfirmationEmail(ApplicationUser user, string confirmationToken)
    {
        var subject = "Confirm your AutoPost email";
        var htmlBody =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>" +
            "<p>Your AutoPost account was created successfully.</p>" +
            $"<p>Use this confirmation token together with your user id <strong>{user.Id}</strong> to confirm your email:</p>" +
            $"<pre>{System.Net.WebUtility.HtmlEncode(confirmationToken)}</pre>";

        var textBody =
            $"Hello {user.DisplayName},{Environment.NewLine}" +
            $"Confirm your AutoPost email using user id {user.Id} and token:{Environment.NewLine}" +
            confirmationToken;

        return new EmailMessage(user.Email!, subject, htmlBody, textBody);
    }

    /// <summary>
    /// Maps the domain user entity to the API-safe profile DTO.
    /// </summary>
    public static UserProfileDto MapUserProfile(ApplicationUser user)
    {
        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.AvatarUrl,
            user.TimeZoneId,
            user.Locale,
            user.IsActive,
            user.LastLoginAt,
            user.RegisteredAt);
    }

    /// <summary>
    /// Builds the Redis cache key used for session list projections.
    /// </summary>
    public static string BuildSessionsCacheKey(Guid userId, bool activeOnly)
    {
        return activeOnly ? $"auth:sessions:active:{userId}" : $"auth:sessions:all:{userId}";
    }

    /// <summary>
    /// Invalidates session-list caches after any mutation of refresh-token state.
    /// </summary>
    public static async Task InvalidateSessionCachesAsync(ICacheService cacheService, Guid userId, CancellationToken cancellationToken)
    {
        await cacheService.RemoveAsync(BuildSessionsCacheKey(userId, activeOnly: false), cancellationToken);
        await cacheService.RemoveAsync(BuildSessionsCacheKey(userId, activeOnly: true), cancellationToken);
    }

    /// <summary>
    /// Maps a refresh-token entity to the session DTO used by the security UI.
    /// </summary>
    public static UserSessionDto MapSession(RefreshToken refreshToken, Guid? currentSessionId)
    {
        return new UserSessionDto(
            refreshToken.Id,
            refreshToken.CreatedAt,
            refreshToken.ExpiresAt,
            refreshToken.RevokedAt,
            refreshToken.DeviceInfo,
            refreshToken.IpAddress,
            currentSessionId.HasValue && currentSessionId.Value == refreshToken.Id);
    }

    private static string GenerateRawRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
