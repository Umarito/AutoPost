using Application.Abstractions.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPublishingJobRepository"/>.
///
/// <para><b>How it works:</b>
/// Each publishing attempt generates one PublishingJob record. Results are ordered by
/// AttemptNumber so the debugging timeline reads chronologically.</para>
///
/// <para><b>Purpose:</b>
/// Provides an audit trail of every API call made to publish a post target.
/// Invaluable for debugging failed publications — stores raw API responses.</para>
/// </summary>
public class PublishingJobRepository(ApplicationDbContext db) : IPublishingJobRepository
{
    /// <summary>
    /// Loads one tracked publishing job for in-place mutation.
    /// </summary>
    public async Task<PublishingJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PublishingJobs.AsNoTracking().FirstOrDefaultAsync(pj => pj.Id == id, ct);

    /// <summary>
    /// Adds the job to the change tracker. Called before making the platform API call.
    /// </summary>
    public async Task<PublishingJob> AddAsync(PublishingJob job, CancellationToken ct = default)
    {
        await db.PublishingJobs.AddAsync(job, ct);
        return job;
    }

    /// <summary>
    /// Lists all publishing attempts for a target, ordered by attempt number (1, 2, 3...).
    /// AsNoTracking — the publishing history timeline is read-only.
    /// </summary>
    public async Task<IReadOnlyList<PublishingJob>> GetByPostTargetIdAsync(Guid postTargetId, CancellationToken ct = default)
        => await db.PublishingJobs.AsNoTracking()
            .Where(pj => pj.PostTargetId == postTargetId)
            .OrderBy(pj => pj.AttemptNumber)
            .ToListAsync(ct);

    /// <summary>
    /// Loads all publishing attempts that belong to a specific post across every target.
    /// </summary>
    public async Task<IReadOnlyList<PublishingJob>> GetByPostIdAsync(Guid postId, CancellationToken ct = default)
        => await db.PublishingJobs.AsNoTracking()
            .Include(job => job.PostTarget)
            .Where(job => job.PostTarget.PostId == postId)
            .OrderBy(job => job.StartedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Loads failed or retrying publishing attempts for a whole workspace.
    /// </summary>
    public async Task<IReadOnlyList<PublishingJob>> GetFailedByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.PublishingJobs.AsNoTracking()
            .Include(job => job.PostTarget)
            .ThenInclude(target => target.Post)
            .Where(job =>
                job.PostTarget.Post.WorkspaceId == workspaceId &&
                (job.Outcome == Domain.Enums.JobOutcome.Failed || job.Outcome == Domain.Enums.JobOutcome.Retrying))
            .OrderByDescending(job => job.StartedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Marks the job as Modified. Called after the API call to record outcome, timing,
    /// HTTP status code, raw API response, and error message.
    /// </summary>
    public void Update(PublishingJob job)
        => db.PublishingJobs.Update(job);
}
