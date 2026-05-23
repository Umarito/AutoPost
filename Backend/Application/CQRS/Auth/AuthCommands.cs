using Application.Common;
using Application.DTOs.Auth;
using MediatR;

namespace Application.CQRS.Auth;

/// <summary>
/// Registers a new user account and initializes the first workspace context.
/// </summary>
/// <param name="Request">Registration payload containing email, password and display name.</param>
public sealed record RegisterUserCommand(RegisterRequest Request) : IRequest<Result<AuthTokensDto>>;

/// <summary>
/// Authenticates a user with email and password and issues JWT and refresh tokens.
/// </summary>
/// <param name="Request">Login payload containing credentials.</param>
public sealed record LoginCommand(LoginRequest Request) : IRequest<Result<AuthTokensDto>>;

/// <summary>
/// Exchanges a valid refresh token for a new short-lived access token and a rotated refresh token.
/// </summary>
/// <param name="RefreshToken">Opaque refresh token supplied by the client, usually from a secure cookie.</param>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

/// <summary>
/// Revokes the current session represented by the supplied refresh token.
/// </summary>
/// <param name="RefreshToken">Opaque refresh token that should no longer be accepted.</param>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

/// <summary>
/// Revokes every active refresh token that belongs to the current user.
/// </summary>
public sealed record LogoutAllDevicesCommand() : IRequest<Result>;

/// <summary>
/// Confirms ownership of the user's email address using the identity-generated confirmation token.
/// </summary>
/// <param name="UserId">Identifier of the user whose email is being confirmed.</param>
/// <param name="Token">Email confirmation token sent to the user.</param>
public sealed record ConfirmEmailCommand(Guid UserId, string Token) : IRequest<Result>;

/// <summary>
/// Re-sends the email confirmation message for an account that is not yet confirmed.
/// </summary>
/// <param name="Email">Email address that should receive a new confirmation message.</param>
public sealed record ResendEmailConfirmationCommand(string Email) : IRequest<Result>;

/// <summary>
/// Updates the current user's profile fields such as display name, avatar, timezone and locale.
/// </summary>
/// <param name="Request">Patch-style profile update payload.</param>
public sealed record UpdateUserProfileCommand(UpdateProfileRequest Request) : IRequest<Result<UserProfileDto>>;

/// <summary>
/// Performs explicit refresh token rotation for a known refresh token.
/// </summary>
/// <param name="RefreshToken">Opaque refresh token that should be rotated.</param>
public sealed record RotateRefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

/// <summary>
/// Revokes a specific refresh token session by its persistence identifier.
/// </summary>
/// <param name="RefreshTokenId">Refresh token record identifier that should be revoked.</param>
public sealed record RevokeRefreshTokenCommand(Guid RefreshTokenId) : IRequest<Result>;

/// <summary>
/// Removes expired refresh token records from persistence to keep the session store healthy.
/// </summary>
public sealed record CleanupExpiredTokensCommand() : IRequest<Result>;
