using Application.Abstractions.Security;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Security;

/// <summary>
/// Computes SHA-256 hashes for refresh token persistence and verification.
/// </summary>
public sealed class RefreshTokenHasher : IRefreshTokenHasher
{
    /// <inheritdoc />
    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public bool Verify(string rawToken, string storedHash)
        => string.Equals(Hash(rawToken), storedHash, StringComparison.OrdinalIgnoreCase);
}
