namespace WebApi.Security;

/// <summary>
/// Provides a single place for writing and deleting refresh token cookies.
/// </summary>
public interface IRefreshTokenCookieService
{
    /// <summary>
    /// Appends the refresh token cookie to the outgoing HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response that should receive the cookie.</param>
    /// <param name="refreshToken">The plaintext refresh token value returned to the client.</param>
    /// <param name="expiresAtUtc">The UTC expiration timestamp of the cookie.</param>
    void AppendRefreshToken(HttpResponse response, string refreshToken, DateTime expiresAtUtc);

    /// <summary>
    /// Deletes the refresh token cookie from the outgoing HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response that should remove the cookie.</param>
    void DeleteRefreshToken(HttpResponse response);
}
