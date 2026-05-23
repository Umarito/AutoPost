using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines persistence operations for posts.
/// </summary>
public interface IPostRepository
{
    /// <summary>
    /// Retrieves a post by its identifier.
    /// </summary>
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a post with its publishing targets.
    /// </summary>
    Task<Post?> GetByIdWithTargetsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists posts of a workspace using optional filters.
    /// </summary>
    Task<IReadOnlyList<Post>> GetByWorkspaceIdAsync(
        Guid workspaceId,
        PostStatus? status = null,
        Platform? platform = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Counts posts of a workspace created during the supplied UTC month window.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="fromInclusiveUtc">Inclusive lower boundary of the month window.</param>
    /// <param name="toExclusiveUtc">Exclusive upper boundary of the month window.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of posts created during the period.</returns>
    Task<int> CountByWorkspaceIdForPeriodAsync(
        Guid workspaceId,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Persists a newly created post.
    /// </summary>
    Task<Post> AddAsync(Post post, CancellationToken ct = default);

    /// <summary>
    /// Marks a post as modified.
    /// </summary>
    void Update(Post post);

    /// <summary>
    /// Removes a post.
    /// </summary>
    void Remove(Post post);

    /// <summary>
    /// Retrieves all posts due for publishing.
    /// </summary>
    Task<IReadOnlyList<Post>> GetScheduledPostsDueAsync(DateTime utcNow, CancellationToken ct = default);
}
