using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines persistence operations for connected social accounts.
/// </summary>
public interface ISocialAccountRepository
{
    /// <summary>
    /// Retrieves a social account by its identifier.
    /// </summary>
    Task<SocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a social account by its identifier together with the owning workspace navigation.
    /// </summary>
    /// <param name="id">Social-account identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The tracked social account with workspace data when found; otherwise <c>null</c>.</returns>
    Task<SocialAccount?> GetByIdWithWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all connected social accounts for a workspace.
    /// </summary>
    Task<IReadOnlyList<SocialAccount>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Finds a connected account by platform-side identifier inside a workspace.
    /// </summary>
    Task<SocialAccount?> GetByExternalIdAsync(Guid workspaceId, Platform platform, string externalAccountId, CancellationToken ct = default);

    /// <summary>
    /// Counts active connected social accounts for a workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of active social accounts.</returns>
    Task<int> CountActiveByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Persists a newly connected social account.
    /// </summary>
    Task<SocialAccount> AddAsync(SocialAccount account, CancellationToken ct = default);

    /// <summary>
    /// Marks a social account as modified.
    /// </summary>
    void Update(SocialAccount account);

    /// <summary>
    /// Removes a social account.
    /// </summary>
    void Remove(SocialAccount account);

    /// <summary>
    /// Lists all accounts with a specific status.
    /// </summary>
    Task<IReadOnlyList<SocialAccount>> GetByStatusAsync(SocialAccountStatus status, CancellationToken ct = default);
}
