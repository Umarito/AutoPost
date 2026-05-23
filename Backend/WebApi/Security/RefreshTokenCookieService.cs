using Microsoft.Extensions.Options;
using WebApi.Options;

namespace WebApi.Security;

/// <summary>
/// Applies the agreed HttpOnly cookie transport rules for refresh tokens.
/// </summary>
public sealed class RefreshTokenCookieService : IRefreshTokenCookieService
{
    private readonly RefreshTokenOptions _refreshTokenOptions;

    /// <summary>
    /// Initializes the service with the configured refresh token cookie options.
    /// </summary>
    /// <param name="refreshTokenOptions">The refresh token cookie options.</param>
    public RefreshTokenCookieService(IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    /// <inheritdoc />
    public void AppendRefreshToken(HttpResponse response, string refreshToken, DateTime expiresAtUtc)
    {
        response.Cookies.Append(
            _refreshTokenOptions.CookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = _refreshTokenOptions.HttpOnly,
                Secure = _refreshTokenOptions.SecurePolicy == CookieSecurePolicy.Always,
                SameSite = _refreshTokenOptions.SameSite,
                Expires = expiresAtUtc,
                Path = _refreshTokenOptions.CookiePath,
                Domain = _refreshTokenOptions.Domain,
                IsEssential = _refreshTokenOptions.IsEssential
            });
    }

    /// <inheritdoc />
    public void DeleteRefreshToken(HttpResponse response)
    {
        response.Cookies.Delete(
            _refreshTokenOptions.CookieName,
            new CookieOptions
            {
                Path = _refreshTokenOptions.CookiePath,
                Domain = _refreshTokenOptions.Domain,
                SameSite = _refreshTokenOptions.SameSite,
                Secure = _refreshTokenOptions.SecurePolicy == CookieSecurePolicy.Always
            });
    }
}
