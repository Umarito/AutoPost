using Application.Common;
using System.Security.Claims;

namespace WebApi.Security;

/// <summary>
/// Reads the current authenticated user and workspace context from JWT claims.
/// </summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes the current user context accessor.
    /// </summary>
    /// <param name="httpContextAccessor">The ASP.NET Core HTTP context accessor.</param>
    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid UserId => TryGetGuidClaim(ClaimTypes.NameIdentifier, "sub") ?? Guid.Empty;

    /// <inheritdoc />
    public Guid WorkspaceId => TryGetGuidClaim("workspace_id") ?? Guid.Empty;

    /// <inheritdoc />
    public Guid? SessionId => TryGetClaim("sid");

    /// <inheritdoc />
    public string Role => TryGetStringClaim("workspace_role", ClaimTypes.Role) ?? string.Empty;

    private Guid? TryGetClaim(string claimType)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private Guid? TryGetGuidClaim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = TryGetClaim(claimType);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private string? TryGetStringClaim(params string[] claimTypes)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
