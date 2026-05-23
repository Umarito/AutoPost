namespace Application.Abstractions.Security;

/// <summary>
/// Decoded workspace invitation token payload.
/// </summary>
/// <param name="WorkspaceId">Workspace that the invite belongs to.</param>
/// <param name="Email">Email address the invitation targets.</param>
/// <param name="Role">Workspace role that should be granted on acceptance.</param>
/// <param name="ExpiresAtUtc">UTC expiration timestamp for the token.</param>
public sealed record InviteTokenPayload(
    Guid WorkspaceId,
    string Email,
    string Role,
    DateTime ExpiresAtUtc);

/// <summary>
/// Decoded email confirmation token payload.
/// </summary>
/// <param name="UserId">User whose email ownership is being confirmed.</param>
/// <param name="Email">Email address being confirmed.</param>
/// <param name="ExpiresAtUtc">UTC expiration timestamp after which the token is no longer valid.</param>
public sealed record EmailConfirmationTokenPayload(
    Guid UserId,
    string Email,
    DateTime ExpiresAtUtc);
