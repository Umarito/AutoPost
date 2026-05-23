namespace Application.Abstractions.Security;

/// <summary>
/// Generates and validates workspace invitation tokens.
///
/// <para><b>Security role:</b>
/// Invitation tokens allow a user to join a workspace with a specific role.
/// They must be tamper-resistant, time-limited and safe to distribute through email links.</para>
/// </summary>
public interface IInviteTokenService
{
    /// <summary>
    /// Creates a signed or protected invitation token for a specific workspace membership.
    /// </summary>
    /// <param name="workspaceId">Workspace the invite grants access to.</param>
    /// <param name="email">Email address the invite is intended for.</param>
    /// <param name="role">Role that should be assigned after acceptance.</param>
    /// <param name="expiresAtUtc">UTC expiration timestamp after which the token is no longer valid.</param>
    /// <returns>Opaque invitation token suitable for transport in an email link.</returns>
    string Generate(Guid workspaceId, string email, string role, DateTime expiresAtUtc);

    /// <summary>
    /// Validates and decodes an invitation token.
    /// </summary>
    /// <param name="token">Opaque invitation token received from the client.</param>
    /// <returns>Decoded invitation payload if the token is valid and unexpired.</returns>
    InviteTokenPayload Validate(string token);
}
