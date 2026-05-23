using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkspaceRepository"/>.
///
/// <para><b>How it works:</b>
/// Injects <see cref="ApplicationDbContext"/> via primary constructor and delegates all persistence
/// operations to EF Core. Read-only queries use <c>AsNoTracking()</c> for better performance
/// (no change tracking overhead). Write operations use tracked entities so that changes are
/// flushed when the Unit of Work calls <c>SaveChangesAsync</c>.</para>
///
/// <para><b>Purpose:</b>
/// Encapsulates all SQL/EF Core logic for Workspace data access behind the
/// <see cref="IWorkspaceRepository"/> interface, keeping the Application layer persistence-ignorant.</para>
/// </summary>
public class WorkspaceRepository(ApplicationDbContext db) : IWorkspaceRepository
{
    /// <summary>
    /// Uses <c>FindAsync</c> which first checks the local identity map (in-memory cache of tracked entities)
    /// before querying the database — optimal for single-entity lookups by primary key.
    /// </summary>
    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Workspaces.FindAsync([id], ct);

    /// <summary>
    /// Queries by the unique Slug column using <c>FirstOrDefaultAsync</c>.
    /// AsNoTracking because the caller only needs to read the data (e.g., resolve a URL slug to a workspace).
    /// </summary>
    public async Task<Workspace?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(w => w.Slug == slug, ct);

    /// <summary>
    /// Adds the entity to the EF Core change tracker in the Added state.
    /// The actual INSERT is deferred until <c>SaveChangesAsync</c> is called on the DbContext.
    /// </summary>
    public async Task<Workspace> AddAsync(Workspace workspace, CancellationToken ct = default)
    {
        await db.Workspaces.AddAsync(workspace, ct);
        return workspace;
    }

    /// <summary>
    /// Attaches the entity and marks it as Modified, ensuring EF Core generates an UPDATE statement.
    /// Useful when the entity was loaded by <c>FindAsync</c> (already tracked) or when re-attaching
    /// a disconnected entity from the Application layer.
    /// </summary>
    public void Update(Workspace workspace)
        => db.Workspaces.Update(workspace);

    /// <summary>
    /// Uses a sub-query through the <c>Members</c> navigation to find workspaces where the user
    /// has a membership record. Translates to SQL: WHERE EXISTS (SELECT 1 FROM WorkspaceMembers WHERE ...).
    /// AsNoTracking because the workspace selector UI is read-only.
    /// </summary>
    public async Task<IReadOnlyList<Workspace>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.Workspaces.AsNoTracking()
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

    /// <summary>
    /// Uses <c>AnyAsync</c> which translates to SELECT EXISTS — the most efficient way to check
    /// for the presence of a row without loading any data.
    /// </summary>
    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => await db.Workspaces.AsNoTracking().AnyAsync(w => w.Slug == slug, ct);
}
