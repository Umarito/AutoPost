using Domain.Entities;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="PublishingJob"/> entity.
///
/// <para><b>Role in the system:</b>
/// PublishingJob logs every individual publishing attempt for a PostTarget.
/// A single PostTarget may have multiple PublishingJobs if the first attempt fails and retries occur.
/// Each record captures the attempt number, timing, outcome (Success/Failed/TimedOut), raw API response,
/// and error messages — invaluable for debugging publishing failures.</para>
///
/// <para><b>TRD reference:</b>
/// Stage 2 — Content &amp; Publications. "PublishingJob — each row logs one publishing attempt."</para>
/// </summary>
public interface IPublishingJobRepository
{
    /// <summary>
    /// Retrieves a publishing job by its identifier.
    /// </summary>
    /// <param name="id">Publishing-job identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The tracked publishing job, or <c>null</c> when the record does not exist.</returns>
    Task<PublishingJob?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Records a new publishing attempt. Called by the Hangfire publisher worker
    /// before making the platform API call, then updated with the result afterwards.
    /// </summary>
    Task<PublishingJob> AddAsync(PublishingJob job, CancellationToken ct = default);

    /// <summary>
    /// Lists all publishing attempts for a specific PostTarget, ordered by attempt number.
    /// Used to render the publishing history (attempt timeline) on the post detail page,
    /// showing each attempt's outcome and API response for debugging.
    /// Results are not tracked (read-only).
    /// </summary>
    Task<IReadOnlyList<PublishingJob>> GetByPostTargetIdAsync(Guid postTargetId, CancellationToken ct = default);

    /// <summary>
    /// Lists all publishing attempts that belong to the targets of a specific post.
    /// </summary>
    /// <param name="postId">Owning post identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>Publishing attempts ordered by their start time.</returns>
    Task<IReadOnlyList<PublishingJob>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);

    /// <summary>
    /// Lists failed or retrying publishing attempts for all post targets in a workspace.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>Failed publishing attempts ordered by newest first.</returns>
    Task<IReadOnlyList<PublishingJob>> GetFailedByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Marks a publishing job as modified. Called after the API call completes to record
    /// the outcome (Success/Failed/TimedOut), completion time, HTTP status code,
    /// raw API response, and error message.
    /// </summary>
    void Update(PublishingJob job);
}
