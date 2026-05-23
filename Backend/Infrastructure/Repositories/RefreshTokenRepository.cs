using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenRepository"/>.
/// </summary>
public class RefreshTokenRepository(ApplicationDbContext db) : IRefreshTokenRepository
{
    /// <inheritdoc />
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    /// <inheritdoc />
    public async Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(Guid userId, bool activeOnly, CancellationToken ct = default)
    {
        var query = db.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.UserId == userId);

        if (activeOnly)
        {
            var utcNow = DateTime.UtcNow;
            query = query.Where(rt => !rt.IsRevoked && !rt.IsUsed && rt.ExpiresAt > utcNow);
        }

        return await query
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<RefreshToken> AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await db.RefreshTokens.AddAsync(token, ct);
        return token;
    }

    /// <inheritdoc />
    public void Update(RefreshToken token)
        => db.RefreshTokens.Update(token);

    /// <inheritdoc />
    public async Task RevokeAllForUserAsync(Guid userId, DateTime revokedAtUtc, CancellationToken ct = default)
        => await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedAt, revokedAtUtc),
                ct);

    /// <inheritdoc />
    public async Task CleanupExpiredAsync(CancellationToken ct = default)
        => await db.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow && (rt.IsUsed || rt.IsRevoked))
            .ExecuteDeleteAsync(ct);
}
