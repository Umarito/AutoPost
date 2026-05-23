using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="Workspace"/> aggregate root.
///
/// <para><b>Role in the system:</b>
/// Workspace is the top-level tenant boundary in AutoPost. Every piece of data — posts,
/// social accounts, automation rules, inbox conversations — belongs to exactly one Workspace.
/// This repository provides the Application layer with a clean, EF-Core-free API to create,
/// read, and update workspaces without coupling business logic to the persistence mechanism.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 1 — Auth &amp; Workspace. Endpoints: GET/PUT /api/workspaces/{id}.</para>
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Retrieves a single workspace by its primary key.
    /// The returned entity is tracked by EF Core, so any property changes will be
    /// persisted when <c>SaveChangesAsync</c> is called on the Unit of Work.
    /// </summary>
    /// <param name="id">The unique identifier of the workspace (Guid).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// The <see cref="Workspace"/> if found; otherwise <c>null</c>.
    /// </returns>
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a workspace by its URL-safe slug.
    /// Used for tenant routing: <c>app.autopost.com/{slug}</c>.
    /// This is a read-only operation — the returned entity is not tracked.
    /// </summary>
    /// <param name="slug">The unique URL slug (e.g., "acme-corp").</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// The <see cref="Workspace"/> matching the slug; otherwise <c>null</c>.
    /// </returns>
    Task<Workspace?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Persists a new workspace to the database.
    /// Called during workspace creation flow after the domain object is constructed
    /// and validated by the Application service.
    /// </summary>
    /// <param name="workspace">The fully initialized workspace entity.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The same <see cref="Workspace"/> instance, now tracked by EF Core with an assigned Id.</returns>
    Task<Workspace> AddAsync(Workspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Marks an existing workspace as modified so EF Core will persist the changes
    /// on the next <c>SaveChangesAsync</c> call.
    /// Typical updates: name, plan, limits, active status.
    /// </summary>
    /// <param name="workspace">The workspace entity with updated properties.</param>
    void Update(Workspace workspace);

    /// <summary>
    /// Lists all workspaces where the given user is a member.
    /// Used to populate the workspace-selector UI after login — the user picks
    /// which workspace to enter, and the JWT is issued with that WorkspaceId.
    /// This is a read-only operation — results are not tracked.
    /// </summary>
    /// <param name="userId">The Id of the authenticated user.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An ordered list of <see cref="Workspace"/> entities the user belongs to,
    /// sorted alphabetically by name.
    /// </returns>
    Task<IReadOnlyList<Workspace>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a slug is already in use by another workspace.
    /// Called during workspace creation to enforce the unique-slug constraint
    /// before hitting a database-level unique index violation.
    /// </summary>
    /// <param name="slug">The slug to validate.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns><c>true</c> if the slug is taken; <c>false</c> if it is available.</returns>
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
}
