using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Video;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  VIDEO DTOs — Upload, Processing Pipeline, Media Library                   ║
// ║  TRD Stage 2: Content & Publications                                       ║
// ║  Endpoints: POST /api/videos/upload, GET /api/videos, DELETE /api/videos   ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for initiating a chunked video upload session.
///
/// <para><b>What it does:</b>
/// Creates an upload session on the server. The server calculates the chunk size
/// and total number of chunks based on the file size. The client then uploads
/// individual chunks via PUT /api/videos/upload/{uploadId}/chunk.</para>
///
/// <para><b>Why chunked:</b>
/// Large video files (100MB+) cannot be uploaded reliably in a single request.
/// Chunked upload enables resumable uploads, progress tracking, and retry per chunk.</para>
///
/// <para><b>TRD API:</b> POST /api/videos/upload/init</para>
/// </summary>
/// <param name="FileName">Original file name with extension (e.g., "review_ep42.mp4"). Used for display in the media library.</param>
/// <param name="ContentType">MIME type of the video file (e.g., "video/mp4"). Validated against allowed types.</param>
/// <param name="FileSizeBytes">Total file size in bytes. Used to calculate chunk count and validate plan limits.</param>
public record InitUploadRequest(
    [Required] string FileName,
    [Required] string ContentType,
    [Required, Range(1, long.MaxValue)] long FileSizeBytes
);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Upload session details returned after initiating a chunked upload.
///
/// <para><b>What the client does with it:</b>
/// Uses the UploadId for subsequent chunk uploads, and ChunkSizeBytes to split the file.
/// The client uploads TotalChunks sequential chunks, each of ChunkSizeBytes (last may be smaller).</para>
/// </summary>
/// <param name="UploadId">Unique identifier for this upload session. Used in chunk upload and completion URLs.</param>
/// <param name="ChunkSizeBytes">Recommended size per chunk in bytes (typically 10MB = 10,485,760).</param>
/// <param name="TotalChunks">Total number of chunks expected based on file size and chunk size.</param>
/// <param name="Status">Session status: "InProgress" after init, "Completed" after finalization.</param>
public record UploadSessionDto(
    Guid UploadId,
    int ChunkSizeBytes,
    int TotalChunks,
    string Status
);

/// <summary>
/// Chunk upload progress returned after each chunk is successfully uploaded.
///
/// <para><b>What the client does with it:</b>
/// Uses UploadedChunks/TotalChunks to show a progress bar. When UploadedChunks == TotalChunks,
/// the client calls POST /api/videos/upload/{uploadId}/complete to finalize.</para>
/// </summary>
/// <param name="UploadId">The upload session identifier.</param>
/// <param name="ChunkIndex">The 0-based index of the chunk that was just uploaded.</param>
/// <param name="UploadedChunks">Total number of chunks uploaded so far.</param>
/// <param name="TotalChunks">Total number of chunks expected.</param>
public record ChunkDto(
    Guid UploadId,
    int ChunkIndex,
    int UploadedChunks,
    int TotalChunks
);

/// <summary>
/// Compact video representation for list views (media library grid).
///
/// <para><b>What it contains:</b>
/// Essential fields for rendering a video thumbnail card: name, size, status, duration.
/// Full details (CDN URL, metadata, uploader) are available via <see cref="VideoDetailDto"/>.</para>
///
/// <para><b>TRD API:</b> GET /api/videos</para>
/// </summary>
/// <param name="Id">The video's unique identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name (e.g., "review_ep42.mp4").</param>
/// <param name="ThumbnailUrl">URL to the auto-generated or custom thumbnail, or null if not yet processed.</param>
/// <param name="FileSizeBytes">File size in bytes (for display: "42.5 MB").</param>
/// <param name="Status">Processing status as string: "Uploading", "Processing", "Ready", "Failed", "Deleted".</param>
/// <param name="DurationSeconds">Video duration in seconds (from FFprobe), or null if not yet analyzed.</param>
/// <param name="UploadedAt">UTC timestamp when the upload was initiated.</param>
public record VideoSummaryDto(
    Guid Id,
    string OriginalFileName,
    string? ThumbnailUrl,
    long FileSizeBytes,
    string Status,
    int? DurationSeconds,
    DateTime UploadedAt
);

/// <summary>
/// Complete video representation with metadata for the video detail page.
///
/// <para><b>What it adds over <see cref="VideoSummaryDto"/>:</b>
/// CDN URL for playback, full technical metadata (resolution, codecs, frame rate),
/// and the uploader's display name.</para>
///
/// <para><b>TRD API:</b> GET /api/videos/{id}</para>
/// </summary>
/// <param name="Id">The video's unique identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="CdnUrl">CDN URL for video playback/download, or null if still processing.</param>
/// <param name="ThumbnailUrl">Thumbnail URL, or null if not yet generated.</param>
/// <param name="FileSizeBytes">File size in bytes.</param>
/// <param name="Status">Processing status as string.</param>
/// <param name="Metadata">Technical metadata from FFprobe analysis, or null if not yet processed.</param>
/// <param name="UploadedAt">UTC timestamp when the upload was initiated.</param>
/// <param name="UploadedByDisplayName">Display name of the team member who uploaded the video.</param>
public record VideoDetailDto(
    Guid Id,
    string OriginalFileName,
    string? CdnUrl,
    string? ThumbnailUrl,
    long FileSizeBytes,
    string Status,
    VideoMetadataDto? Metadata,
    DateTime UploadedAt,
    string UploadedByDisplayName
);

/// <summary>
/// Technical video metadata extracted by FFprobe during the processing pipeline.
///
/// <para><b>What it contains:</b>
/// All technical properties needed for platform compatibility checks:
/// resolution, aspect ratio, codecs, frame rate, and audio presence.</para>
///
/// <para><b>Why it matters:</b>
/// Each platform has specific requirements. For example, TikTok requires 9:16 aspect ratio,
/// YouTube prefers 16:9, and Instagram Reels requires specific codec support.
/// The PlatformValidation service uses these values to warn users before publishing.</para>
/// </summary>
/// <param name="DurationSeconds">Video duration in whole seconds.</param>
/// <param name="Width">Horizontal resolution in pixels (e.g., 1920).</param>
/// <param name="Height">Vertical resolution in pixels (e.g., 1080).</param>
/// <param name="AspectRatio">Aspect ratio as human-readable string: "16:9", "9:16", "1:1", "4:5".</param>
/// <param name="FrameRate">Frame rate (e.g., 30.0, 60.0, 29.97).</param>
/// <param name="VideoCodec">Video codec name (e.g., "h264", "h265", "vp9").</param>
/// <param name="AudioCodec">Audio codec name (e.g., "aac", "opus"), or null if no audio track.</param>
/// <param name="HasAudio">Whether the video contains an audio track.</param>
public record VideoMetadataDto(
    int DurationSeconds,
    int Width,
    int Height,
    string AspectRatio,
    double FrameRate,
    string VideoCodec,
    string? AudioCodec,
    bool HasAudio
);
