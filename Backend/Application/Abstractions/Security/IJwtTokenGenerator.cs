using Domain.Entities;
using System.Security.Claims;

namespace Application.Abstractions.Security;

/// <summary>
/// Generates short-lived JWT access tokens for authenticated API users.
///
/// <para>
/// The generator creates signed bearer tokens that carry the current user,
/// workspace and role context required by the Web API authorization pipeline.
/// </para>
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Creates a signed JWT access token for the supplied user and workspace context.
    /// </summary>
    /// <param name="user">The authenticated application user.</param>
    /// <param name="workspaceId">The tenant boundary that should be embedded into the token.</param>
    /// <param name="workspaceRole">The user's role inside the current workspace.</param>
    /// <param name="additionalClaims">Optional additional claims that should be added to the token.</param>
    /// <returns>The serialized JWT access token string.</returns>
    string GenerateAccessToken(
        ApplicationUser user,
        Guid workspaceId,
        string workspaceRole,
        IEnumerable<Claim>? additionalClaims = null);

    /// <summary>
    /// Gets the UTC expiration timestamp that will be assigned to newly generated access tokens.
    /// </summary>
    /// <returns>The UTC expiration timestamp for a newly generated token.</returns>
    DateTime GetAccessTokenExpiresAtUtc();
}
