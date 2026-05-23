namespace Application.Abstractions.Media;

/// <summary>
/// Extracts technical metadata from uploaded video assets.
///
/// <para><b>Role in the system:</b>
/// The video pipeline relies on normalized metadata such as duration, resolution,
/// codecs and aspect ratio to validate platform compatibility before content is
/// scheduled or published.</para>
/// </summary>
public interface IVideoMetadataExtractor
{
    /// <summary>
    /// Extracts normalized video metadata from the supplied media asset.
    /// </summary>
    /// <param name="mediaUri">Address of the uploaded media asset in storage.</param>
    /// <param name="ct">Cancellation token for potentially expensive I/O or subprocess execution.</param>
    /// <returns>Technical metadata required by scheduling and publishing flows.</returns>
    Task<VideoMetadataExtractionResult> ExtractAsync(Uri mediaUri, CancellationToken ct = default);
}
