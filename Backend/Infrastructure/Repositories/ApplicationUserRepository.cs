using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IApplicationUserRepository"/>.
///
/// <para><b>How it works:</b>
/// Handles domain-specific user data operations (profile, activity timestamps).
/// Identity operations (password, lockout, 2FA) are NOT here — they live in ASP.NET Core's
/// <c>UserManager</c>. Uses <c>ExecuteUpdateAsync</c> for targeted single-column updates
/// to avoid loading the full user entity graph just to change one property.</para>
///
/// <para><b>Purpose:</b>
/// Separates domain user queries from Identity infrastructure, keeping the Application layer
/// clean and testable without depending on UserManager.</para>
/// </summary>
public class ApplicationUserRepository(ApplicationDbContext db) : IApplicationUserRepository
{
    /// <summary>
    /// Uses <c>FindAsync</c> for primary key lookup — checks the local identity map first,
    /// then falls back to the database. Returns a tracked entity for subsequent updates.
    /// </summary>
    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ApplicationUsers.FindAsync([id], ct);

    /// <summary>
    /// Queries by email using <c>FirstOrDefaultAsync</c> with AsNoTracking.
    /// Used during login flow before handing off to Identity for password verification.
    /// </summary>
    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await db.ApplicationUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    /// <summary>
    /// Adds the user to the EF Core change tracker. The actual INSERT happens when
    /// <c>SaveChangesAsync</c> is called. Note: for user creation with password,
    /// prefer <c>UserManager.CreateAsync</c> which handles hashing.
    /// </summary>
    public async Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken ct = default)
    {
        await db.ApplicationUsers.AddAsync(user, ct);
        return user;
    }

    /// <summary>
    /// Marks the entire entity as Modified for EF Core to generate an UPDATE statement.
    /// </summary>
    public void Update(ApplicationUser user)
        => db.ApplicationUsers.Update(user);

    /// <summary>
    /// Uses <c>ExecuteUpdateAsync</c> — a direct SQL UPDATE that bypasses change tracking.
    /// This is significantly more efficient than loading the full user entity just to update one timestamp.
    /// Translates to: UPDATE AspNetUsers SET LastLoginAt = @p0 WHERE Id = @p1.
    /// </summary>
    public async Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt, CancellationToken ct = default)
        => await db.ApplicationUsers
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, lastLoginAt), ct);

    /// <summary>
    /// Uses <c>AnyAsync</c> (SELECT EXISTS) for the most efficient existence check.
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await db.ApplicationUsers.AsNoTracking().AnyAsync(u => u.Email == email, ct);
}
