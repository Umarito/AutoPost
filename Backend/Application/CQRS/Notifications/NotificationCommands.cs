using Application.Common;
using Application.DTOs.Notification;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Notifications;

/// <summary>
/// Creates or updates one notification preference for the current user.
/// </summary>
/// <param name="Request">Preference update payload.</param>
public sealed record UpdateNotificationPreferenceCommand(UpdateNotificationPreferenceRequest Request) : IRequest<Result<NotificationPreferenceDto>>;

/// <summary>
/// Replaces or updates the full notification preference set for the current user.
/// </summary>
/// <param name="Request">Bulk preference update payload.</param>
public sealed record UpdateAllNotificationPreferencesCommand(BulkUpdateNotificationPreferencesRequest Request) : IRequest<Result<IReadOnlyList<NotificationPreferenceDto>>>;

/// <summary>
/// Sends a notification through the channels enabled for the target user and event type.
/// </summary>
/// <param name="UserId">User that should receive the notification.</param>
/// <param name="EventType">Logical event type driving preference checks.</param>
/// <param name="Title">Short notification title.</param>
/// <param name="Body">Main notification body.</param>
/// <param name="ActionUrl">Optional deep link or UI route.</param>
public sealed record SendNotificationCommand(
    Guid UserId,
    Guid WorkspaceId,
    NotificationEventType EventType,
    string Title,
    string Body,
    string? ActionUrl) : IRequest<Result>;
