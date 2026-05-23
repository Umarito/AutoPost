namespace Application.Abstractions.Media;

/// <summary>
/// Coordinates non-trivial video processing work such as thumbnail generation,
/// transcoding and metadata enrichment.
///
/// <para><b>Performance role:</b>
/// These operations are typically delegated to Hangfire background jobs because
/// they may involve large files, external tools and CPU-heavy processing.</para>
/// </summary>
public interface IVideoProcessingService
{
    /// <summary>
    /// Processes an uploaded video so it becomes ready for use in the media library and publishing flows.
    /// </summary>
    /// <param name="videoId">The uploaded video that should be processed.</param>
    /// <param name="ct">Cancellation token for the background processing workflow.</param>
    /// <returns>A task that completes when the processing workflow has finished.</returns>
    Task ProcessAsync(Guid videoId, CancellationToken ct = default);

    /// <summary>
    /// Generates or refreshes a thumbnail for a previously uploaded video.
    /// </summary>
    /// <param name="videoId">The video whose thumbnail should be generated.</param>
    /// <param name="ct">Cancellation token for the underlying I/O or processing work.</param>
    /// <returns>The URL or blob name of the generated thumbnail.</returns>
    Task<string> GenerateThumbnailAsync(Guid videoId, CancellationToken ct = default);
}
