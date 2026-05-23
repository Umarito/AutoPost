using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="ApplicationUser"/> aggregate root.
///
/// <para><b>Role in the system:</b>
/// ApplicationUser represents a human who can log in, join workspaces, create posts,
/// and manage automation rules. This repository handles domain-specific user data
/// (display name, avatar, timezone, locale, activity timestamps). Identity-specific
/// operations (password hashing, lockout, two-factor) are handled by ASP.NET Core's
/// <c>UserManager&lt;ApplicationUser&gt;</c>, not by this repository.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 1 — Auth &amp; Workspace. Endpoints: POST /api/auth/register, POST /api/auth/login.</para>
/// </summary>
public interface IApplicationUserRepository
{
    /// <summary>
    /// Retrieves a user by their primary key (Guid).
    /// The returned entity is tracked, allowing property updates to be flushed
    /// on the next <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="id">The unique user identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The <see cref="ApplicationUser"/> if found; otherwise <c>null</c>.</returns>
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a user by their email address.
    /// Used during login to resolve the user before verifying their password via Identity.
    /// This is a read-only operation — the result is not tracked.
    /// </summary>
    /// <param name="email">The email address to search for (case-insensitive in PostgreSQL).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The <see cref="ApplicationUser"/> matching the email; otherwise <c>null</c>.</returns>
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Persists a new user record to the database.
    /// Called after the registration command handler constructs and validates the entity.
    /// </summary>
    /// <param name="user">The fully initialized user entity.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The same <see cref="ApplicationUser"/> instance, now tracked by EF Core.</returns>
    Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken ct = default);

    /// <summary>
    /// Marks an existing user as modified so EF Core persists the changes.
    /// Typical updates: display name, avatar URL, timezone, locale, IsActive flag.
    /// </summary>
    /// <param name="user">The user entity with updated properties.</param>
    void Update(ApplicationUser user);

    /// <summary>
    /// Updates only the <c>LastLoginAt</c> timestamp using a direct SQL UPDATE statement
    /// (<c>ExecuteUpdateAsync</c>) for efficiency — avoids loading the full entity graph.
    /// Called after every successful authentication.
    /// </summary>
    /// <param name="userId">The Id of the user who just logged in.</param>
    /// <param name="lastLoginAt">The UTC timestamp of the login event.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt, CancellationToken ct = default);

    /// <summary>
    /// Checks whether an email is already registered in the system.
    /// Called during registration to provide a user-friendly validation error
    /// before hitting the database-level unique constraint.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the email is taken; <c>false</c> if it is available.</returns>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}
