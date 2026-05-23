using Application.Common;
using Application.DTOs.Notification;
using MediatR;

namespace Application.CQRS.Notifications;

/// <summary>
/// Retrieves the current notification preference set for the user in the active workspace.
/// </summary>
public sealed record GetNotificationPreferencesQuery() : IRequest<Result<IReadOnlyList<NotificationPreferenceDto>>>;

/// <summary>
/// Retrieves paginated notification delivery history for the current user.
/// </summary>
/// <param name="Pagination">Standard pagination request.</param>
public sealed record GetNotificationHistoryQuery(PagedRequest Pagination) : IRequest<Result<PagedResult<NotificationHistoryDto>>>;
