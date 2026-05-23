using Domain.Entities;
using Domain.Enums;

namespace Application.Abstractions.Repositories;

/// <summary>
/// Defines the data-access contract for the <see cref="Video"/> aggregate root.
/// Videos are media files uploaded to the workspace library. They are independent from posts —
/// one video can be reused across multiple publications.
/// TRD: "Soft Delete — physical deletion from Storage is a separate scheduled job."
/// TRD Stage 2 — Content &amp; Publications. Endpoints: POST/GET/DELETE /api/videos.
/// </summary>
public interface IVideoRepository
{
    /// <summary>
    /// Retrieves a video by Id (respects the soft-delete GlobalQueryFilter).
    /// The entity is tracked for updates (status, metadata after FFprobe).
    /// </summary>
    Task<Video?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all non-deleted videos in a workspace, ordered by upload date (newest first).
    /// Used to render the media library UI. Results are not tracked.
    /// </summary>
    Task<IReadOnlyList<Video>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Persists a newly uploaded video record. Called after the file is stored in Blob Storage
    /// and basic metadata (filename, content type, size) is captured.
    /// </summary>
    Task<Video> AddAsync(Video video, CancellationToken ct = default);

    /// <summary>
    /// Marks a video as modified. Typical updates: status transitions (Uploading → Processing → Ready),
    /// VideoMetadata population after FFprobe, CDN URL assignment.
    /// </summary>
    void Update(Video video);

    /// <summary>
    /// Retrieves videos by processing status. Used by background jobs for pipeline monitoring
    /// (e.g., find all "Processing" videos to check FFprobe completion).
    /// </summary>
    Task<IReadOnlyList<Video>> GetByStatusAsync(VideoStatus status, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a video by Id while bypassing the soft-delete GlobalQueryFilter.
    /// Used by the physical cleanup Hangfire job that deletes files from Blob Storage
    /// for videos whose <c>DeletedAt</c> is older than the retention period.
    /// </summary>
    Task<Video?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default);
}
