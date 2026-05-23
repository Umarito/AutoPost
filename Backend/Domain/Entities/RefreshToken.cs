using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Represents one refresh token backed user session.
/// </summary>
public class RefreshToken : BaseEntity<Guid>
{
    /// <summary>
    /// Gets the owner user identifier of the session.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hash of the raw refresh token.
    /// </summary>
    public string TokenHash { get; private set; } = default!;

    /// <summary>
    /// Gets optional device or browser information captured at issuance time.
    /// </summary>
    public string? DeviceInfo { get; private set; }

    /// <summary>
    /// Gets the originating IP address captured at issuance time.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC expiration timestamp after which the token must no longer be accepted.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the token has already been consumed during rotation.
    /// </summary>
    public bool IsUsed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the token has been revoked manually or due to theft detection.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the token was explicitly revoked, if any.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Gets the user navigation owning the session.
    /// </summary>
    public ApplicationUser User { get; private set; } = default!;

    /// <summary>
    /// Creates a new refresh token session record.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="tokenHash">SHA-256 hash of the raw refresh token.</param>
    /// <param name="createdAtUtc">UTC issuance timestamp.</param>
    /// <param name="expiresAtUtc">UTC expiration timestamp.</param>
    /// <param name="deviceInfo">Optional device description for the session UI.</param>
    /// <param name="ipAddress">Optional originating IP address for audit.</param>
    /// <returns>A new <see cref="RefreshToken"/> entity.</returns>
    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? deviceInfo,
        string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo) ? null : deviceInfo.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            CreatedAt = createdAtUtc,
            ExpiresAt = expiresAtUtc,
            IsUsed = false,
            IsRevoked = false
        };
    }

    /// <summary>
    /// Determines whether the token is still valid for refresh rotation.
    /// </summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><c>true</c> when the token can still be used; otherwise <c>false</c>.</returns>
    public bool IsActive(DateTime utcNow)
    {
        return !IsUsed && !IsRevoked && ExpiresAt > utcNow;
    }

    /// <summary>
    /// Marks the token as consumed during a successful rotation flow.
    /// </summary>
    public void MarkUsed()
    {
        IsUsed = true;
    }

    /// <summary>
    /// Revokes the token and captures the UTC revocation timestamp.
    /// </summary>
    /// <param name="revokedAtUtc">UTC revocation timestamp.</param>
    public void Revoke(DateTime revokedAtUtc)
    {
        IsRevoked = true;
        RevokedAt = revokedAtUtc;
    }
}
