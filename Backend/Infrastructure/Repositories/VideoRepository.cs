using Application.Abstractions.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IVideoRepository"/>.
///
/// <para><b>How it works:</b>
/// The Video entity has a GlobalQueryFilter (<c>v.DeletedAt == null</c>) applied in the configuration,
/// so all standard queries automatically exclude soft-deleted videos. The special method
/// <c>GetByIdIncludingDeletedAsync</c> uses <c>IgnoreQueryFilters()</c> to bypass this filter.</para>
///
/// <para><b>Purpose:</b>
/// Manages the media library — upload tracking, processing pipeline status, and soft-delete lifecycle.</para>
/// </summary>
public class VideoRepository(ApplicationDbContext db) : IVideoRepository
{
    /// <summary>
    /// Uses <c>AsNoTracking</c> with <c>FirstOrDefaultAsync</c> for read-only lookups.
    /// The GlobalQueryFilter ensures soft-deleted videos are excluded.
    /// </summary>
    public async Task<Video?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    /// <summary>
    /// Lists non-deleted videos for a workspace, ordered by upload date (newest first).
    /// AsNoTracking — the media library grid is read-only.
    /// </summary>
    public async Task<IReadOnlyList<Video>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default)
        => await db.Videos.AsNoTracking()
            .Where(v => v.WorkspaceId == workspaceId)
            .OrderByDescending(v => v.UploadedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Adds the video to the change tracker. Actual INSERT on SaveChangesAsync.
    /// </summary>
    public async Task<Video> AddAsync(Video video, CancellationToken ct = default)
    {
        await db.Videos.AddAsync(video, ct);
        return video;
    }

    /// <summary>
    /// Marks the video as Modified for status transitions and metadata population.
    /// </summary>
    public void Update(Video video)
        => db.Videos.Update(video);

    /// <summary>
    /// Queries videos by processing status. Used by background pipeline jobs
    /// (e.g., find all "Processing" videos to check FFprobe completion).
    /// AsNoTracking — the job re-fetches individually for tracked updates.
    /// </summary>
    public async Task<IReadOnlyList<Video>> GetByStatusAsync(VideoStatus status, CancellationToken ct = default)
        => await db.Videos.AsNoTracking()
            .Where(v => v.Status == status)
            .ToListAsync(ct);

    /// <summary>
    /// Uses <c>IgnoreQueryFilters()</c> to bypass the soft-delete GlobalQueryFilter.
    /// This is the ONLY method that can see deleted videos — used by the cleanup Hangfire job
    /// to find videos whose DeletedAt is past the retention period and delete them from Blob Storage.
    /// Uses <c>AsNoTracking</c> to avoid change tracking overhead.
    /// </summary>
    public async Task<Video?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => await db.Videos.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, ct);
}
