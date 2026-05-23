using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="PostTarget"/> entity.
///
/// <para><b>Role in the system:</b>
/// PostTarget is a child entity of Post — it tracks the publishing status for each social platform
/// independently. A single Post can have multiple PostTargets (e.g., one for Instagram, one for YouTube).
/// Each PostTarget records whether it was published successfully or failed, along with the platform-side
/// post URL and error details.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 2 — Content &amp; Publications. PostTarget tracks per-platform publishing outcomes.</para>
/// </summary>
public interface IPostTargetRepository
{
    /// <summary>
    /// Retrieves a post target by its primary key, including its SocialAccount navigation.
    /// Used when updating the publishing result for a specific platform after the API call completes.
    /// The entity is tracked for updates.
    /// </summary>
    Task<PostTarget?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all targets for a specific post with their SocialAccount details.
    /// Used to render the per-platform status breakdown on the post detail page.
    /// Results are not tracked (read-only).
    /// </summary>
    Task<IReadOnlyList<PostTarget>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new post target entry. Called when a post is created and the user
    /// selects which social accounts to publish to.
    /// </summary>
    Task<PostTarget> AddAsync(PostTarget target, CancellationToken ct = default);

    /// <summary>
    /// Marks a post target as modified. Typical updates: status transitions
    /// (Pending → Publishing → Published/Failed), setting the platform-side post URL,
    /// and populating the PostTargetResult owned entity with error or success details.
    /// </summary>
    void Update(PostTarget target);
}
