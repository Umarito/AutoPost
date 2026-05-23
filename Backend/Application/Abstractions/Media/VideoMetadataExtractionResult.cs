namespace Application.Abstractions.Media;

/// <summary>
/// Technical metadata extracted from a video asset for downstream validation and presentation.
/// </summary>
/// <param name="DurationSeconds">Duration of the media in whole seconds.</param>
/// <param name="Width">Video width in pixels.</param>
/// <param name="Height">Video height in pixels.</param>
/// <param name="AspectRatio">Human-readable aspect ratio value such as <c>16:9</c> or <c>9:16</c>.</param>
/// <param name="FrameRate">Frame rate reported by the processing pipeline.</param>
/// <param name="VideoCodec">Detected video codec.</param>
/// <param name="AudioCodec">Detected audio codec, if present.</param>
/// <param name="VideoBitrate">Detected video bitrate in bits per second when available.</param>
/// <param name="HasAudio">Whether an audio track was detected.</param>
public sealed record VideoMetadataExtractionResult(
    int DurationSeconds,
    int Width,
    int Height,
    string AspectRatio,
    double FrameRate,
    string VideoCodec,
    string? AudioCodec,
    long VideoBitrate,
    bool HasAudio);
