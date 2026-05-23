namespace Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies refresh tokens before persistence.
///
/// <para>
/// The TRD requires refresh tokens to be stored only as SHA-256 hashes so that
/// database leakage does not expose live session secrets. This contract centralizes
/// that behavior behind an application-facing abstraction.
/// </para>
/// </summary>
public interface IRefreshTokenHasher
{
    /// <summary>
    /// Computes a stable hash for a raw refresh token.
    /// </summary>
    /// <param name="rawToken">The plaintext refresh token generated for the client.</param>
    /// <returns>The hexadecimal SHA-256 hash representation.</returns>
    string Hash(string rawToken);

    /// <summary>
    /// Verifies that a raw refresh token matches a previously stored hash.
    /// </summary>
    /// <param name="rawToken">The raw token presented by the client.</param>
    /// <param name="storedHash">The hash stored in the database.</param>
    /// <returns><c>true</c> when the token matches; otherwise <c>false</c>.</returns>
    bool Verify(string rawToken, string storedHash);
}
