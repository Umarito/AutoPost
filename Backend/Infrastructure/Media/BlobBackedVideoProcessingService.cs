using System.Diagnostics;
using Application.Abstractions.Media;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Storage;
using Domain.Entities;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media;

/// <summary>
/// Processes uploaded videos by reading the blob asset, extracting metadata and generating thumbnails.
/// </summary>
public sealed class BlobBackedVideoProcessingService : IVideoProcessingService
{
    private readonly IVideoRepository _videoRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IVideoMetadataExtractor _videoMetadataExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AzureBlobStorageOptions _storageOptions;
    private readonly ILogger<BlobBackedVideoProcessingService> _logger;

    /// <summary>
    /// Initializes the video-processing service.
    /// </summary>
    /// <param name="videoRepository">Repository used to load and update video aggregates.</param>
    /// <param name="blobStorageService">Blob storage adapter used to read raw media and upload thumbnails.</param>
    /// <param name="videoMetadataExtractor">Technical metadata extractor.</param>
    /// <param name="unitOfWork">Unit of work used to persist processing state.</param>
    /// <param name="storageOptions">Blob storage options containing logical container names.</param>
    /// <param name="logger">Structured logger for diagnostics.</param>
    public BlobBackedVideoProcessingService(
        IVideoRepository videoRepository,
        IBlobStorageService blobStorageService,
        IVideoMetadataExtractor videoMetadataExtractor,
        IUnitOfWork unitOfWork,
        IOptions<AzureBlobStorageOptions> storageOptions,
        ILogger<BlobBackedVideoProcessingService> logger)
    {
        _videoRepository = videoRepository;
        _blobStorageService = blobStorageService;
        _videoMetadataExtractor = videoMetadataExtractor;
        _unitOfWork = unitOfWork;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(Guid videoId, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(videoId, ct)
            ?? throw new InvalidOperationException($"Video '{videoId}' was not found.");

        try
        {
            video.MarkProcessing();
            _videoRepository.Update(video);
            await _unitOfWork.SaveChangesAsync(ct);

            var localVideoPath = await MaterializeVideoAsync(video, ct);
            try
            {
                var metadataResult = await _videoMetadataExtractor.ExtractAsync(new Uri(localVideoPath), ct);
                video.SetMetadata(VideoMetadata.Create(
                    metadataResult.DurationSeconds,
                    metadataResult.Width,
                    metadataResult.Height,
                    metadataResult.AspectRatio,
                    metadataResult.FrameRate,
                    metadataResult.VideoCodec,
                    metadataResult.AudioCodec,
                    metadataResult.VideoBitrate,
                    metadataResult.HasAudio));

                video.SetCdnUrl(_blobStorageService.GetBlobUri(_storageOptions.VideosContainerName, video.StorageKey).ToString());

                if (string.IsNullOrWhiteSpace(video.ThumbnailUrl))
                {
                    var thumbnailUrl = await GenerateThumbnailInternalAsync(video, localVideoPath, ct);
                    if (!string.IsNullOrWhiteSpace(thumbnailUrl))
                    {
                        video.SetThumbnail(thumbnailUrl);
                    }
                }

                video.MarkReady();
                _videoRepository.Update(video);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            finally
            {
                TryDeleteFile(localVideoPath);
            }
        }
        catch (Exception exception)
        {
            video.MarkFailed(exception.Message);
            _videoRepository.Update(video);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogError(exception, "Unexpected failure while processing video {VideoId}.", videoId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateThumbnailAsync(Guid videoId, CancellationToken ct = default)
    {
        var video = await _videoRepository.GetByIdAsync(videoId, ct)
            ?? throw new InvalidOperationException($"Video '{videoId}' was not found.");

        var localVideoPath = await MaterializeVideoAsync(video, ct);
        try
        {
            var thumbnailUrl = await GenerateThumbnailInternalAsync(video, localVideoPath, ct);
            if (!string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                video.SetThumbnail(thumbnailUrl);
                _videoRepository.Update(video);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return thumbnailUrl;
        }
        finally
        {
            TryDeleteFile(localVideoPath);
        }
    }

    private async Task<string> MaterializeVideoAsync(Video video, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{video.Id}{Path.GetExtension(video.OriginalFileName)}");
        await using var source = await _blobStorageService.OpenReadAsync(_storageOptions.VideosContainerName, video.StorageKey, ct);
        await using var target = File.Create(tempPath);
        await source.CopyToAsync(target, ct);
        return tempPath;
    }

    private async Task<string> GenerateThumbnailInternalAsync(Video video, string localVideoPath, CancellationToken ct)
    {
        var tempThumbnailPath = Path.Combine(Path.GetTempPath(), $"{video.Id}.jpg");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{localVideoPath}\" -ss 00:00:01 -vframes 1 \"{tempThumbnailPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            _ = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(tempThumbnailPath))
            {
                _logger.LogWarning(
                    "ffmpeg thumbnail generation failed for video {VideoId}. ExitCode: {ExitCode}. Error: {Error}",
                    video.Id,
                    process.ExitCode,
                    error);

                return video.ThumbnailUrl ?? video.CdnUrl ?? string.Empty;
            }

            await using var thumbnailStream = File.OpenRead(tempThumbnailPath);
            var blobName = $"{video.WorkspaceId}/{video.Id}.jpg";
            var uploadResult = await _blobStorageService.UploadAsync(
                _storageOptions.ThumbnailsContainerName,
                blobName,
                thumbnailStream,
                "image/jpeg",
                ct);

            return uploadResult.BlobUri.ToString();
        }
        finally
        {
            TryDeleteFile(tempThumbnailPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
