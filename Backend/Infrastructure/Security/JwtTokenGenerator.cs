using Application.Abstractions.Security;
using Domain.Entities;
using Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Security;

/// <summary>
/// Generates signed JWT access tokens that carry the authenticated workspace context.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes the token generator with the configured JWT options.
    /// </summary>
    /// <param name="jwtOptions">The JWT options bound from configuration.</param>
    public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc />
    public string GenerateAccessToken(
        ApplicationUser user,
        Guid workspaceId,
        string workspaceRole,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var expiresAtUtc = GetAccessTokenExpiresAtUtc();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id.ToString()),
            new("workspace_id", workspaceId.ToString()),
            new("workspace_role", workspaceRole),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public DateTime GetAccessTokenExpiresAtUtc()
        => DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);
}
