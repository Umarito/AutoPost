using Application.Common;
using Application.DTOs.Auth;
using MediatR;

namespace Application.CQRS.Auth;

/// <summary>
/// Retrieves the public profile information for a specific user.
/// </summary>
/// <param name="UserId">User identifier whose profile should be loaded.</param>
public sealed record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;

/// <summary>
/// Retrieves all sessions associated with the current user, including revoked ones when needed for audit.
/// </summary>
public sealed record GetUserSessionsQuery() : IRequest<Result<IReadOnlyList<UserSessionDto>>>;

/// <summary>
/// Retrieves only currently active sessions for the current user.
/// </summary>
public sealed record GetActiveSessionsQuery() : IRequest<Result<IReadOnlyList<UserSessionDto>>>;
