using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines persistence operations for refresh-token backed user sessions.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Retrieves a refresh token by its unique SHA-256 hash.
    /// </summary>
    /// <param name="tokenHash">SHA-256 hash of the raw refresh token.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The matching tracked <see cref="RefreshToken"/> with the owning user loaded, or <c>null</c>.</returns>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a refresh token by its persistence identifier.
    /// </summary>
    /// <param name="id">Refresh token record identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The tracked <see cref="RefreshToken"/> with the owning user loaded, or <c>null</c>.</returns>
    Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists refresh tokens belonging to one user.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="activeOnly">Whether only active sessions should be returned.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An ordered list of refresh token records.</returns>
    Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(Guid userId, bool activeOnly, CancellationToken ct = default);

    /// <summary>
    /// Persists a newly issued refresh token.
    /// </summary>
    /// <param name="token">Refresh token entity to add.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The same tracked <see cref="RefreshToken"/> instance.</returns>
    Task<RefreshToken> AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>
    /// Marks a tracked refresh token as modified.
    /// </summary>
    /// <param name="token">Refresh token entity carrying updated state.</param>
    void Update(RefreshToken token);

    /// <summary>
    /// Revokes every active token for a user in one bulk operation.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="revokedAtUtc">UTC timestamp of the revocation event.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task RevokeAllForUserAsync(Guid userId, DateTime revokedAtUtc, CancellationToken ct = default);

    /// <summary>
    /// Deletes expired and already inactive refresh tokens.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
