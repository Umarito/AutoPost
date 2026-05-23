using System.Diagnostics;
using System.Text.Json;
using Application.Abstractions.Media;

namespace Infrastructure.Media;

/// <summary>
/// Uses the ffprobe CLI to extract technical metadata from uploaded video assets.
/// </summary>
public sealed class FfprobeVideoMetadataExtractor : IVideoMetadataExtractor
{
    /// <inheritdoc />
    public async Task<VideoMetadataExtractionResult> ExtractAsync(Uri mediaUri, CancellationToken ct = default)
    {
        if (!mediaUri.IsFile)
        {
            throw new InvalidOperationException("The ffprobe metadata extractor expects a local file URI.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -show_entries stream=width,height,codec_name,avg_frame_rate,bit_rate:format=duration -of json \"{mediaUri.LocalPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe exited with code {process.ExitCode}: {error}");
        }

        using var document = JsonDocument.Parse(output);
        var streams = document.RootElement.TryGetProperty("streams", out var streamArray) ? streamArray : default;
        var firstVideoStream = streams.EnumerateArray().FirstOrDefault();
        var format = document.RootElement.TryGetProperty("format", out var formatElement) ? formatElement : default;

        var width = firstVideoStream.TryGetProperty("width", out var widthElement) ? widthElement.GetInt32() : 0;
        var height = firstVideoStream.TryGetProperty("height", out var heightElement) ? heightElement.GetInt32() : 0;
        var videoCodec = firstVideoStream.TryGetProperty("codec_name", out var codecElement) ? codecElement.GetString() ?? "unknown" : "unknown";
        var frameRate = ParseFrameRate(firstVideoStream.TryGetProperty("avg_frame_rate", out var frameRateElement) ? frameRateElement.GetString() : null);
        var bitrate = firstVideoStream.TryGetProperty("bit_rate", out var bitrateElement) && long.TryParse(bitrateElement.GetString(), out var parsedBitrate)
            ? parsedBitrate
            : 0L;
        var durationSeconds = format.TryGetProperty("duration", out var durationElement) && double.TryParse(durationElement.GetString(), out var duration)
            ? (int)Math.Round(duration)
            : 0;

        return new VideoMetadataExtractionResult(
            durationSeconds,
            width,
            height,
            BuildAspectRatio(width, height),
            frameRate,
            videoCodec,
            null,
            bitrate,
            false);
    }

    private static double ParseFrameRate(string? frameRate)
    {
        if (string.IsNullOrWhiteSpace(frameRate))
        {
            return 0;
        }

        var parts = frameRate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var numerator) &&
            double.TryParse(parts[1], out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(frameRate, out var parsed) ? parsed : 0;
    }

    private static string BuildAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return "unknown";
        }

        var gcd = GreatestCommonDivisor(width, height);
        return $"{width / gcd}:{height / gcd}";
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            var temp = right;
            right = left % right;
            left = temp;
        }

        return Math.Abs(left);
    }
}
