using Application.Abstractions.Caching;
using Application.Abstractions.Notifications;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Notification;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Notifications;

/// <summary>
/// Возвращает набор настроек уведомлений текущего пользователя в активном workspace.
/// При отсутствии persisted-настроек хендлер отдает детерминированный набор MVP-defaults без создания дубликатов.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler : IRequestHandler<GetNotificationPreferencesQuery, Result<IReadOnlyList<NotificationPreferenceDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetNotificationPreferencesQueryHandler> _logger;

    /// <summary>
    /// Инициализирует хендлер чтения настроек уведомлений.
    /// </summary>
    public GetNotificationPreferencesQueryHandler(
        ICurrentUserContext currentUserContext,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetNotificationPreferencesQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<NotificationPreferenceDto>>> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<NotificationPreferenceDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cacheKey = NotificationWorkflow.BuildPreferencesCacheKey(_currentUserContext.UserId, _currentUserContext.WorkspaceId);
            var cached = await _cacheService.GetAsync<IReadOnlyList<NotificationPreferenceDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<IReadOnlyList<NotificationPreferenceDto>>.Ok(cached);
            }

            var preferences = await _notificationPreferenceRepository.GetByUserAndWorkspaceAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                cancellationToken);

            var effectivePreferences = preferences.Count == 0
                ? NotificationPreference.CreateDefaults(_currentUserContext.UserId, _currentUserContext.WorkspaceId)
                : preferences;

            var dto = effectivePreferences
                .Select(preference => NotificationWorkflow.MapPreferenceDto(_mapper, preference))
                .OrderBy(item => item.EventType)
                .ToArray();

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);
            return Result<IReadOnlyList<NotificationPreferenceDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading notification preferences for user {UserId} in workspace {WorkspaceId}.", _currentUserContext.UserId, _currentUserContext.WorkspaceId);
            return Result<IReadOnlyList<NotificationPreferenceDto>>.Fail("An unexpected notification-preferences lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Выполняет upsert одной настройки уведомлений текущего пользователя.
/// </summary>
public sealed class UpdateNotificationPreferenceCommandHandler : IRequestHandler<UpdateNotificationPreferenceCommand, Result<NotificationPreferenceDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateNotificationPreferenceCommandHandler> _logger;

    /// <summary>
    /// Инициализирует хендлер обновления одной настройки уведомлений.
    /// </summary>
    public UpdateNotificationPreferenceCommandHandler(
        ICurrentUserContext currentUserContext,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<UpdateNotificationPreferenceCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<NotificationPreferenceDto>> Handle(UpdateNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<NotificationPreferenceDto>.Fail(access.Error!, access.Code!.Value);
            }

            var preference = await _notificationPreferenceRepository.GetByUserWorkspaceAndEventAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                request.Request.EventType,
                cancellationToken);

            if (preference is null)
            {
                preference = NotificationPreference.Create(
                    _currentUserContext.UserId,
                    _currentUserContext.WorkspaceId,
                    request.Request.EventType,
                    request.Request.InAppEnabled,
                    request.Request.EmailEnabled,
                    request.Request.PushEnabled);

                await _notificationPreferenceRepository.AddAsync(preference, cancellationToken);
            }
            else
            {
                preference.UpdateChannels(
                    request.Request.InAppEnabled,
                    request.Request.EmailEnabled,
                    request.Request.PushEnabled);

                _notificationPreferenceRepository.Update(preference);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await NotificationWorkflow.InvalidatePreferenceCacheAsync(_cacheService, _currentUserContext.UserId, _currentUserContext.WorkspaceId, cancellationToken);

            return Result<NotificationPreferenceDto>.Ok(NotificationWorkflow.MapPreferenceDto(_mapper, preference));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while updating notification preference {EventType} for user {UserId}.", request.Request.EventType, _currentUserContext.UserId);
            return Result<NotificationPreferenceDto>.Fail("An unexpected notification-preference update error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Применяет bulk-обновление настроек уведомлений, поддерживая preset и явный список изменений из TRD.
/// </summary>
public sealed class UpdateAllNotificationPreferencesCommandHandler : IRequestHandler<UpdateAllNotificationPreferencesCommand, Result<IReadOnlyList<NotificationPreferenceDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateAllNotificationPreferencesCommandHandler> _logger;

    /// <summary>
    /// Инициализирует хендлер массового обновления настроек уведомлений.
    /// </summary>
    public UpdateAllNotificationPreferencesCommandHandler(
        ICurrentUserContext currentUserContext,
        INotificationPreferenceRepository notificationPreferenceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<UpdateAllNotificationPreferencesCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<NotificationPreferenceDto>>> Handle(UpdateAllNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<NotificationPreferenceDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var existing = (await _notificationPreferenceRepository.GetByUserAndWorkspaceAsync(
                    _currentUserContext.UserId,
                    _currentUserContext.WorkspaceId,
                    cancellationToken))
                .ToDictionary(preference => preference.EventType);

            var updates = NotificationWorkflow.BuildBulkUpdateSet(request.Request);
            foreach (var update in updates)
            {
                if (!existing.TryGetValue(update.EventType, out var preference))
                {
                    preference = NotificationPreference.Create(
                        _currentUserContext.UserId,
                        _currentUserContext.WorkspaceId,
                        update.EventType,
                        update.InAppEnabled,
                        update.EmailEnabled,
                        update.PushEnabled);

                    await _notificationPreferenceRepository.AddAsync(preference, cancellationToken);
                    existing[update.EventType] = preference;
                }
                else
                {
                    preference.UpdateChannels(update.InAppEnabled, update.EmailEnabled, update.PushEnabled);
                    _notificationPreferenceRepository.Update(preference);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await NotificationWorkflow.InvalidatePreferenceCacheAsync(_cacheService, _currentUserContext.UserId, _currentUserContext.WorkspaceId, cancellationToken);

            var dto = existing.Values
                .Select(preference => NotificationWorkflow.MapPreferenceDto(_mapper, preference))
                .OrderBy(item => item.EventType)
                .ToArray();

            return Result<IReadOnlyList<NotificationPreferenceDto>>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while bulk-updating notification preferences for user {UserId}.", _currentUserContext.UserId);
            return Result<IReadOnlyList<NotificationPreferenceDto>>.Fail("An unexpected bulk notification-preferences error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Диспетчеризует уведомление по включенным каналам пользователя и пишет persisted delivery history.
/// Хендлер не содержит HTTP-логики и возвращает Result, готовый для будущего WebApi-преобразования в ProblemDetails.
/// </summary>
public sealed class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, Result>
{
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly INotificationHistoryRepository _notificationHistoryRepository;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IEmailService _emailService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    /// <summary>
    /// Инициализирует хендлер отправки уведомлений.
    /// </summary>
    public SendNotificationCommandHandler(
        INotificationPreferenceRepository notificationPreferenceRepository,
        INotificationHistoryRepository notificationHistoryRepository,
        IRealtimeNotificationService realtimeNotificationService,
        IEmailService emailService,
        IPushNotificationService pushNotificationService,
        IApplicationUserRepository applicationUserRepository,
        IUnitOfWork unitOfWork,
        ILogger<SendNotificationCommandHandler> logger)
    {
        _notificationPreferenceRepository = notificationPreferenceRepository;
        _notificationHistoryRepository = notificationHistoryRepository;
        _realtimeNotificationService = realtimeNotificationService;
        _emailService = emailService;
        _pushNotificationService = pushNotificationService;
        _applicationUserRepository = applicationUserRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _applicationUserRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                return Result.Fail("Notification recipient was not found.", ErrorCode.NotFound);
            }

            var preference = await _notificationPreferenceRepository.GetByUserWorkspaceAndEventAsync(
                request.UserId,
                request.WorkspaceId,
                request.EventType,
                cancellationToken);

            var effectivePreference = preference ?? NotificationPreference.CreateDefaults(request.UserId, request.WorkspaceId)
                .First(item => item.EventType == request.EventType);

            var utcNow = DateTime.UtcNow;
            var pendingHistories = NotificationWorkflow.CreateHistoryEntries(
                request.UserId,
                request.WorkspaceId,
                request.EventType,
                request.Title,
                request.Body,
                request.ActionUrl,
                effectivePreference,
                utcNow);

            foreach (var history in pendingHistories)
            {
                await _notificationHistoryRepository.AddAsync(history, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var history in pendingHistories)
            {
                try
                {
                    switch (history.Channel)
                    {
                        case NotificationChannel.InApp:
                            await _realtimeNotificationService.NotifyUserAsync(
                                request.UserId,
                                "notification.received",
                                new
                                {
                                    history.Id,
                                    EventType = request.EventType.ToString(),
                                    request.Title,
                                    request.Body,
                                    request.ActionUrl,
                                    history.CreatedAt
                                },
                                cancellationToken);
                            break;
                        case NotificationChannel.Email:
                            if (!string.IsNullOrWhiteSpace(user.Email))
                            {
                                await _emailService.SendAsync(
                                    new EmailMessage(
                                        user.Email,
                                        request.Title,
                                        $"<p>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>",
                                        request.Body),
                                    cancellationToken);
                            }
                            break;
                        case NotificationChannel.Push:
                            await _pushNotificationService.SendAsync(
                                new PushNotificationMessage(request.UserId, request.Title, request.Body, request.ActionUrl),
                                cancellationToken);
                            break;
                    }

                    history.MarkDelivered(DateTime.UtcNow);
                    _notificationHistoryRepository.Update(history);
                }
                catch (Exception exception)
                {
                    history.MarkFailed(exception.Message);
                    _notificationHistoryRepository.Update(history);
                    _logger.LogWarning(
                        exception,
                        "Notification channel {Channel} failed for user {UserId} and event {EventType}.",
                        history.Channel,
                        request.UserId,
                        request.EventType);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while sending notification {EventType} to user {UserId}.", request.EventType, request.UserId);
            return Result.Fail("An unexpected notification-dispatch error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Возвращает persisted историю уведомлений текущего пользователя в активном workspace.
/// </summary>
public sealed class GetNotificationHistoryQueryHandler : IRequestHandler<GetNotificationHistoryQuery, Result<PagedResult<NotificationHistoryDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INotificationHistoryRepository _notificationHistoryRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetNotificationHistoryQueryHandler> _logger;

    /// <summary>
    /// Инициализирует хендлер истории уведомлений.
    /// </summary>
    public GetNotificationHistoryQueryHandler(
        ICurrentUserContext currentUserContext,
        INotificationHistoryRepository notificationHistoryRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper,
        ILogger<GetNotificationHistoryQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _notificationHistoryRepository = notificationHistoryRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<NotificationHistoryDto>>> Handle(GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PagedResult<NotificationHistoryDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;
            var items = await _notificationHistoryRepository.GetPagedByUserAndWorkspaceAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                skip,
                request.Pagination.PageSize,
                cancellationToken);

            var total = await _notificationHistoryRepository.CountByUserAndWorkspaceAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                cancellationToken);

            var dto = items
                .Select(history => _mapper.Map<NotificationHistoryDto>(history))
                .ToArray();

            return Result<PagedResult<NotificationHistoryDto>>.Ok(
                new PagedResult<NotificationHistoryDto>(dto, total, request.Pagination.Page, request.Pagination.PageSize));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading notification history for user {UserId} in workspace {WorkspaceId}.", _currentUserContext.UserId, _currentUserContext.WorkspaceId);
            return Result<PagedResult<NotificationHistoryDto>>.Fail("An unexpected notification-history lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Содержит вспомогательную логику notification batch: cache keys, presets, DTO mapping и стандартные default-значения.
/// </summary>
internal static class NotificationWorkflow
{
    private static readonly IReadOnlyDictionary<NotificationEventType, string> EventDescriptions = new Dictionary<NotificationEventType, string>
    {
        [NotificationEventType.PostPublished] = "When a post is published successfully.",
        [NotificationEventType.PostFailed] = "When a post fails to publish.",
        [NotificationEventType.NewInboxMessage] = "When a new inbox message or comment arrives.",
        [NotificationEventType.AutomationTriggered] = "When an automation rule triggers or is executed.",
        [NotificationEventType.SocialAccountDisconnected] = "When a connected social account loses authorization.",
        [NotificationEventType.TeamMemberInvited] = "When a workspace invitation is created or updated."
    };

    internal static string BuildPreferencesCacheKey(Guid userId, Guid workspaceId)
        => $"notifications:preferences:{userId}:{workspaceId}";

    internal static Task InvalidatePreferenceCacheAsync(ICacheService cacheService, Guid userId, Guid workspaceId, CancellationToken ct)
        => cacheService.RemoveAsync(BuildPreferencesCacheKey(userId, workspaceId), ct);

    internal static NotificationPreferenceDto MapPreferenceDto(IMapper mapper, NotificationPreference preference)
    {
        var dto = mapper.Map<NotificationPreferenceDto>(preference);
        return dto with
        {
            EventTypeDescription = EventDescriptions.TryGetValue(preference.EventType, out var description)
                ? description
                : preference.EventType.ToString()
        };
    }

    internal static IReadOnlyList<UpdateNotificationPreferenceRequest> BuildBulkUpdateSet(BulkUpdateNotificationPreferencesRequest request)
    {
        var updates = new Dictionary<NotificationEventType, UpdateNotificationPreferenceRequest>();

        if (!string.IsNullOrWhiteSpace(request.Preset))
        {
            foreach (var presetItem in BuildPreset(request.Preset))
            {
                updates[presetItem.EventType] = presetItem;
            }
        }

        if (request.Preferences is not null)
        {
            foreach (var preference in request.Preferences)
            {
                updates[preference.EventType] = preference;
            }
        }

        return updates.Values.ToArray();
    }

    internal static IReadOnlyList<NotificationHistory> CreateHistoryEntries(
        Guid userId,
        Guid workspaceId,
        NotificationEventType eventType,
        string title,
        string body,
        string? actionUrl,
        NotificationPreference preference,
        DateTime createdAtUtc)
    {
        var items = new List<NotificationHistory>();

        if (preference.InAppEnabled)
        {
            items.Add(NotificationHistory.Create(userId, workspaceId, eventType, NotificationChannel.InApp, title, body, actionUrl, createdAtUtc));
        }

        if (preference.EmailEnabled)
        {
            items.Add(NotificationHistory.Create(userId, workspaceId, eventType, NotificationChannel.Email, title, body, actionUrl, createdAtUtc));
        }

        if (preference.PushEnabled)
        {
            items.Add(NotificationHistory.Create(userId, workspaceId, eventType, NotificationChannel.Push, title, body, actionUrl, createdAtUtc));
        }

        return items;
    }

    private static IReadOnlyList<UpdateNotificationPreferenceRequest> BuildPreset(string preset)
    {
        var normalized = preset.Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" => Enum.GetValues<NotificationEventType>()
                .Select(eventType => new UpdateNotificationPreferenceRequest(eventType, true, true, true))
                .ToArray(),
            "inapponly" => Enum.GetValues<NotificationEventType>()
                .Select(eventType => new UpdateNotificationPreferenceRequest(eventType, true, false, false))
                .ToArray(),
            _ => Enum.GetValues<NotificationEventType>()
                .Select(eventType => new UpdateNotificationPreferenceRequest(
                    eventType,
                    true,
                    eventType is NotificationEventType.PostFailed or NotificationEventType.SocialAccountDisconnected or NotificationEventType.TeamMemberInvited,
                    false))
                .ToArray()
        };
    }
}
