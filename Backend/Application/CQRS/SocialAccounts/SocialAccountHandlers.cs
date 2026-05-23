using System.Text.Json;
using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Security;
using Application.BackgroundJobs;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Analytics;
using Application.DTOs.SocialAccount;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.SocialAccounts;

/// <summary>
/// Generates provider authorization URLs while protecting the OAuth state payload against tampering.
/// </summary>
public sealed class GetOAuthUrlQueryHandler : IRequestHandler<GetOAuthUrlQuery, Result<OAuthUrlDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPlatformIntegrationService _platformIntegrationService;
    private readonly ITokenProtectionService _tokenProtectionService;
    private readonly ILogger<GetOAuthUrlQueryHandler> _logger;

    /// <summary>
    /// Initializes the OAuth-URL query handler.
    /// </summary>
    public GetOAuthUrlQueryHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPlatformIntegrationService platformIntegrationService,
        ITokenProtectionService tokenProtectionService,
        ILogger<GetOAuthUrlQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _platformIntegrationService = platformIntegrationService;
        _tokenProtectionService = tokenProtectionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<OAuthUrlDto>> Handle(GetOAuthUrlQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (_currentUserContext.UserId == Guid.Empty)
            {
                return Result<OAuthUrlDto>.Fail("Current user was not found.", ErrorCode.Unauthorized);
            }

            var access = await ContentGuard.RequireManagementAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<OAuthUrlDto>.Fail(access.Error!, access.Code!.Value);
            }

            var statePayload = new OAuthStatePayload(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                request.Platform,
                request.RedirectUri,
                DateTime.UtcNow.AddMinutes(15));

            var protectedState = _tokenProtectionService.Protect(
                JsonSerializer.Serialize(statePayload),
                SocialAccountWorkflow.OAuthStatePurpose);

            var authorizationUrl = _platformIntegrationService.BuildAuthorizationUrl(
                request.Platform,
                request.RedirectUri,
                protectedState);

            return Result<OAuthUrlDto>.Ok(new OAuthUrlDto(authorizationUrl, protectedState));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while building OAuth URL for platform {Platform}.", request.Platform);
            return Result<OAuthUrlDto>.Fail("An unexpected OAuth bootstrap error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Completes the OAuth callback flow, persists or updates the connected account and schedules the first insight collection.
/// </summary>
public sealed class HandleOAuthCallbackCommandHandler : IRequestHandler<HandleOAuthCallbackCommand, Result<SocialAccountDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPlatformIntegrationService _platformIntegrationService;
    private readonly ITokenProtectionService _tokenProtectionService;
    private readonly ICacheService _cacheService;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly IMapper _mapper;
    private readonly ILogger<HandleOAuthCallbackCommandHandler> _logger;

    /// <summary>
    /// Initializes the OAuth callback handler.
    /// </summary>
    public HandleOAuthCallbackCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ISocialAccountRepository socialAccountRepository,
        IUnitOfWork unitOfWork,
        IPlatformIntegrationService platformIntegrationService,
        ITokenProtectionService tokenProtectionService,
        ICacheService cacheService,
        IBackgroundJobScheduler backgroundJobScheduler,
        IMapper mapper,
        ILogger<HandleOAuthCallbackCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _socialAccountRepository = socialAccountRepository;
        _unitOfWork = unitOfWork;
        _platformIntegrationService = platformIntegrationService;
        _tokenProtectionService = tokenProtectionService;
        _cacheService = cacheService;
        _backgroundJobScheduler = backgroundJobScheduler;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SocialAccountDto>> Handle(HandleOAuthCallbackCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (_currentUserContext.UserId == Guid.Empty)
            {
                return Result<SocialAccountDto>.Fail("Current user was not found.", ErrorCode.Unauthorized);
            }

            var statePayload = SocialAccountWorkflow.UnprotectState(request.State, request.Platform, _tokenProtectionService);
            if (statePayload is null ||
                statePayload.UserId != _currentUserContext.UserId ||
                statePayload.WorkspaceId != _currentUserContext.WorkspaceId ||
                !string.Equals(statePayload.RedirectUri, request.RedirectUri, StringComparison.OrdinalIgnoreCase) ||
                statePayload.ExpiresAtUtc < DateTime.UtcNow)
            {
                return Result<SocialAccountDto>.Fail("OAuth state is invalid or expired.", ErrorCode.Validation);
            }

            var access = await ContentGuard.RequireManagementAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<SocialAccountDto>.Fail(access.Error!, access.Code!.Value);
            }

            var workspace = await _workspaceRepository.GetByIdAsync(_currentUserContext.WorkspaceId, cancellationToken);
            if (workspace is null)
            {
                return ContentGuard.NotFound<SocialAccountDto>("Workspace");
            }

            var connection = await _platformIntegrationService.ExchangeCodeAsync(
                request.Platform,
                request.AuthorizationCode,
                request.RedirectUri,
                cancellationToken);

            var existing = await _socialAccountRepository.GetByExternalIdAsync(
                workspace.Id,
                request.Platform,
                connection.Profile.ExternalAccountId,
                cancellationToken);

            var utcNow = DateTime.UtcNow;
            SocialAccount account;

            if (existing is null)
            {
                var connectedCount = await _socialAccountRepository.CountActiveByWorkspaceIdAsync(workspace.Id, cancellationToken);
                if (connectedCount >= workspace.MaxSocialAccounts)
                {
                    return Result<SocialAccountDto>.Fail("Workspace social-account limit has been reached.", ErrorCode.Conflict);
                }

                account = SocialAccount.Connect(
                    workspace.Id,
                    request.Platform,
                    connection.Profile.ExternalAccountId,
                    connection.Profile.DisplayName,
                    connection.Profile.Username,
                    connection.Profile.AvatarUrl,
                    _tokenProtectionService.Protect(connection.AccessToken, SocialAccountWorkflow.GetAccessTokenPurpose(request.Platform)),
                    string.IsNullOrWhiteSpace(connection.RefreshToken)
                        ? null
                        : _tokenProtectionService.Protect(connection.RefreshToken, SocialAccountWorkflow.GetRefreshTokenPurpose(request.Platform)),
                    connection.ExpiresAtUtc,
                    connection.GrantedScopesCsv,
                    utcNow,
                    connection.Profile.AccountType,
                    connection.Profile.IsPrivateAccount,
                    connection.Profile.FollowersCount);

                await _socialAccountRepository.AddAsync(account, cancellationToken);
            }
            else
            {
                existing.UpdateCredentials(
                    _tokenProtectionService.Protect(connection.AccessToken, SocialAccountWorkflow.GetAccessTokenPurpose(request.Platform)),
                    string.IsNullOrWhiteSpace(connection.RefreshToken)
                        ? existing.EncryptedRefreshToken
                        : _tokenProtectionService.Protect(connection.RefreshToken, SocialAccountWorkflow.GetRefreshTokenPurpose(request.Platform)),
                    connection.ExpiresAtUtc,
                    connection.GrantedScopesCsv);

                existing.RefreshProfile(
                    connection.Profile.DisplayName,
                    connection.Profile.Username,
                    connection.Profile.AvatarUrl,
                    connection.Profile.AccountType,
                    connection.Profile.IsPrivateAccount,
                    connection.Profile.FollowersCount,
                    utcNow);

                _socialAccountRepository.Update(existing);
                account = existing;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SocialAccountWorkflow.InvalidateCachesAsync(_cacheService, workspace.Id, account.Id, cancellationToken);

            _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                dispatcher => dispatcher.CollectAccountInsightAsync(account.Id),
                queue: "default");

            return Result<SocialAccountDto>.Ok(_mapper.Map<SocialAccountDto>(account));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while handling OAuth callback for platform {Platform}.", request.Platform);
            return Result<SocialAccountDto>.Fail("An unexpected OAuth callback error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Disconnects a social account while preserving audit history and invalidating cached projections.
/// </summary>
public sealed class DisconnectSocialAccountCommandHandler : IRequestHandler<DisconnectSocialAccountCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DisconnectSocialAccountCommandHandler> _logger;

    /// <summary>
    /// Initializes the disconnect handler.
    /// </summary>
    public DisconnectSocialAccountCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<DisconnectSocialAccountCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(DisconnectSocialAccountCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
            if (account is null)
            {
                return ContentGuard.NotFound("Social account");
            }

            var access = await ContentGuard.RequireManagementAccessAsync(
                _currentUserContext.UserId,
                account.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result.Fail(access.Error!, access.Code!.Value);
            }

            account.Disconnect(DateTime.UtcNow);
            _socialAccountRepository.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SocialAccountWorkflow.InvalidateCachesAsync(_cacheService, account.WorkspaceId, account.Id, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while disconnecting social account {SocialAccountId}.", request.SocialAccountId);
            return Result.Fail("An unexpected social-account disconnection error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Validates and refreshes provider credentials for a connected social account when necessary.
/// </summary>
public sealed class EnsureTokenValidCommandHandler : IRequestHandler<EnsureTokenValidCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPlatformTokenValidationService _platformTokenValidationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnsureTokenValidCommandHandler> _logger;

    /// <summary>
    /// Initializes the token-validation handler.
    /// </summary>
    public EnsureTokenValidCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPlatformTokenValidationService platformTokenValidationService,
        IUnitOfWork unitOfWork,
        ILogger<EnsureTokenValidCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _platformTokenValidationService = platformTokenValidationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(EnsureTokenValidCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
            if (account is null)
            {
                return ContentGuard.NotFound("Social account");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireManagementAccessAsync(
                    _currentUserContext.UserId,
                    account.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            var validation = await _platformTokenValidationService.EnsureValidAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return validation.IsValid
                ? Result.Ok()
                : Result.Fail(validation.FailureReason ?? "Provider credentials are not valid.", ErrorCode.ExternalApi);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while validating social account token {SocialAccountId}.", request.SocialAccountId);
            return Result.Fail("An unexpected token-validation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns the social accounts connected to the current workspace, backed by Redis cache for repeated reads.
/// </summary>
public sealed class GetSocialAccountsQueryHandler : IRequestHandler<GetSocialAccountsQuery, Result<IReadOnlyList<SocialAccountDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetSocialAccountsQueryHandler> _logger;

    /// <summary>
    /// Initializes the social-account query handler.
    /// </summary>
    public GetSocialAccountsQueryHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetSocialAccountsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SocialAccountDto>>> Handle(GetSocialAccountsQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserContext.WorkspaceId;
        var cacheKey = SocialAccountWorkflow.BuildAccountsCacheKey(workspaceId);

        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                workspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<SocialAccountDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cached = await _cacheService.GetAsync<IReadOnlyList<SocialAccountDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<SocialAccountDto>>.Ok(cached);
            }

            var accounts = await _socialAccountRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
            var dto = accounts.Select(_mapper.Map<SocialAccountDto>).ToArray();
            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<IReadOnlyList<SocialAccountDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading workspace social accounts for workspace {WorkspaceId}.", workspaceId);
            return Result<IReadOnlyList<SocialAccountDto>>.Fail("An unexpected social-accounts lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Refreshes cached public profile metadata for a connected account and invalidates dependent read models.
/// </summary>
public sealed class RefreshSocialAccountMetaCommandHandler : IRequestHandler<RefreshSocialAccountMetaCommand, Result<SocialAccountDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPlatformIntegrationService _platformIntegrationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<RefreshSocialAccountMetaCommandHandler> _logger;

    /// <summary>
    /// Initializes the metadata-refresh handler.
    /// </summary>
    public RefreshSocialAccountMetaCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPlatformIntegrationService platformIntegrationService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<RefreshSocialAccountMetaCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _platformIntegrationService = platformIntegrationService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SocialAccountDto>> Handle(RefreshSocialAccountMetaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
            if (account is null)
            {
                return ContentGuard.NotFound<SocialAccountDto>("Social account");
            }

            var access = await ContentGuard.RequireManagementAccessAsync(
                _currentUserContext.UserId,
                account.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<SocialAccountDto>.Fail(access.Error!, access.Code!.Value);
            }

            var profile = await _platformIntegrationService.GetAccountProfileAsync(account, cancellationToken);
            account.RefreshProfile(
                profile.DisplayName,
                profile.Username,
                profile.AvatarUrl,
                profile.AccountType,
                profile.IsPrivateAccount,
                profile.FollowersCount,
                DateTime.UtcNow);

            _socialAccountRepository.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SocialAccountWorkflow.InvalidateCachesAsync(_cacheService, account.WorkspaceId, account.Id, cancellationToken);

            return Result<SocialAccountDto>.Ok(_mapper.Map<SocialAccountDto>(account));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while refreshing social account metadata {SocialAccountId}.", request.SocialAccountId);
            return Result<SocialAccountDto>.Fail("An unexpected social-account metadata refresh error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Collects one account-level analytics snapshot and invalidates growth projections.
/// </summary>
public sealed class CollectAccountInsightCommandHandler : IRequestHandler<CollectAccountInsightCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly ISocialAccountInsightRepository _socialAccountInsightRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IPlatformIntegrationService _platformIntegrationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CollectAccountInsightCommandHandler> _logger;

    /// <summary>
    /// Initializes the insight-collection handler.
    /// </summary>
    public CollectAccountInsightCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        ISocialAccountInsightRepository socialAccountInsightRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IPlatformIntegrationService platformIntegrationService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<CollectAccountInsightCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _socialAccountInsightRepository = socialAccountInsightRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _platformIntegrationService = platformIntegrationService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(CollectAccountInsightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
            if (account is null)
            {
                return ContentGuard.NotFound("Social account");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireReadAccessAsync(
                    _currentUserContext.UserId,
                    account.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            var insight = await _platformIntegrationService.GetAccountInsightAsync(account, cancellationToken);
            var snapshot = SocialAccountInsight.Create(
                account.Id,
                insight.RecordedAtUtc,
                insight.FollowersCount,
                insight.FollowingCount,
                insight.TotalPostsCount,
                insight.Reach,
                insight.Impressions);

            account.RefreshProfile(
                account.AccountDisplayName,
                account.AccountUsername,
                account.AccountAvatarUrl,
                account.AccountType,
                account.IsPrivateAccount,
                insight.FollowersCount,
                insight.RecordedAtUtc);

            await _socialAccountInsightRepository.AddAsync(snapshot, cancellationToken);
            _socialAccountRepository.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SocialAccountWorkflow.InvalidateCachesAsync(_cacheService, account.WorkspaceId, account.Id, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while collecting social-account insight {SocialAccountId}.", request.SocialAccountId);
            return Result.Fail("An unexpected social-account insight collection error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns account-growth time series for a connected social account, backed by Redis cache.
/// </summary>
public sealed class GetAccountGrowthQueryHandler : IRequestHandler<GetAccountGrowthQuery, Result<IReadOnlyList<InsightDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly ISocialAccountInsightRepository _socialAccountInsightRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetAccountGrowthQueryHandler> _logger;

    /// <summary>
    /// Initializes the growth query handler.
    /// </summary>
    public GetAccountGrowthQueryHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        ISocialAccountInsightRepository socialAccountInsightRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetAccountGrowthQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _socialAccountInsightRepository = socialAccountInsightRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InsightDto>>> Handle(GetAccountGrowthQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = SocialAccountWorkflow.BuildGrowthCacheKey(request.SocialAccountId, request.From, request.To);

        try
        {
            var account = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
            if (account is null)
            {
                return ContentGuard.NotFound<IReadOnlyList<InsightDto>>("Social account");
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                account.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<InsightDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cached = await _cacheService.GetAsync<IReadOnlyList<InsightDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<InsightDto>>.Ok(cached);
            }

            var snapshots = await _socialAccountInsightRepository.GetByAccountIdInRangeAsync(
                request.SocialAccountId,
                request.From,
                request.To,
                cancellationToken);

            var dto = snapshots.Select(_mapper.Map<InsightDto>).ToArray();
            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);
            return Result<IReadOnlyList<InsightDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading account growth for social account {SocialAccountId}.", request.SocialAccountId);
            return Result<IReadOnlyList<InsightDto>>.Fail("An unexpected account-growth lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Protected OAuth state payload used to bind authorization responses to the authenticated workspace session.
/// </summary>
/// <param name="UserId">Authenticated user that initiated the OAuth flow.</param>
/// <param name="WorkspaceId">Workspace in which the integration should be connected.</param>
/// <param name="Platform">Target provider platform.</param>
/// <param name="RedirectUri">Redirect URI originally used during authorization.</param>
/// <param name="ExpiresAtUtc">UTC expiration timestamp for CSRF protection.</param>
internal sealed record OAuthStatePayload(
    Guid UserId,
    Guid WorkspaceId,
    Domain.Enums.Platform Platform,
    string RedirectUri,
    DateTime ExpiresAtUtc);

/// <summary>
/// Shared helper methods for social-account handlers.
/// </summary>
internal static class SocialAccountWorkflow
{
    internal const string OAuthStatePurpose = "oauth-state";

    internal static string BuildAccountsCacheKey(Guid workspaceId) => $"social-accounts:{workspaceId}";

    internal static string BuildGrowthCacheKey(Guid socialAccountId, DateTime from, DateTime to)
        => $"social-account-growth:{socialAccountId}:{from:O}:{to:O}";

    internal static string GetAccessTokenPurpose(Domain.Enums.Platform platform) => $"{platform}:access-token";

    internal static string GetRefreshTokenPurpose(Domain.Enums.Platform platform) => $"{platform}:refresh-token";

    internal static OAuthStatePayload? UnprotectState(
        string protectedState,
        Domain.Enums.Platform platform,
        ITokenProtectionService tokenProtectionService)
    {
        try
        {
            var json = tokenProtectionService.Unprotect(protectedState, OAuthStatePurpose);
            var payload = JsonSerializer.Deserialize<OAuthStatePayload>(json);
            return payload?.Platform == platform ? payload : null;
        }
        catch
        {
            return null;
        }
    }

    internal static async Task InvalidateCachesAsync(
        ICacheService cacheService,
        Guid workspaceId,
        Guid socialAccountId,
        CancellationToken ct)
    {
        await cacheService.RemoveAsync(BuildAccountsCacheKey(workspaceId), ct);
        await cacheService.SetAsync($"analytics:stamp:{workspaceId}", Guid.NewGuid().ToString("N"), TimeSpan.FromDays(30), ct);
    }
}
