using Application.Common;
using Application.DTOs.Video;
using Domain.Enums;
using MediatR;

namespace Application.CQRS.Videos;

/// <summary>
/// Starts a resumable upload session for a new video asset.
/// </summary>
/// <param name="Request">Upload initialization payload containing the file name, type and size.</param>
public sealed record InitVideoUploadCommand(InitUploadRequest Request) : IRequest<Result<UploadSessionDto>>;

/// <summary>
/// Uploads one chunk of a previously initialized video upload session.
/// </summary>
/// <param name="UploadId">Upload session identifier.</param>
/// <param name="ChunkIndex">Zero-based position of the uploaded chunk.</param>
/// <param name="Content">Binary content stream for the chunk being uploaded.</param>
public sealed record UploadVideoChunkCommand(Guid UploadId, int ChunkIndex, Stream Content) : IRequest<Result<ChunkDto>>;

/// <summary>
/// Finalizes an upload session and persists the uploaded video in the media library.
/// </summary>
/// <param name="UploadId">Upload session identifier that should be finalized.</param>
public sealed record CompleteVideoUploadCommand(Guid UploadId) : IRequest<Result<VideoDetailDto>>;

/// <summary>
/// Triggers asynchronous video processing for a previously uploaded video.
/// </summary>
/// <param name="VideoId">Video that should be processed.</param>
public sealed record ProcessVideoCommand(Guid VideoId) : IRequest<Result>;

/// <summary>
/// Soft-deletes a video so it is no longer available for future posts.
/// </summary>
/// <param name="VideoId">Video that should be deleted.</param>
public sealed record DeleteVideoCommand(Guid VideoId) : IRequest<Result>;

/// <summary>
/// Generates or refreshes a thumbnail for an uploaded video.
/// </summary>
/// <param name="VideoId">Video whose thumbnail should be generated.</param>
public sealed record GenerateVideoThumbnailCommand(Guid VideoId) : IRequest<Result<string>>;

/// <summary>
/// Persists normalized metadata for a video after processing completes.
/// </summary>
/// <param name="VideoId">Video whose metadata should be updated.</param>
/// <param name="Metadata">Normalized technical metadata extracted from the media file.</param>
public sealed record SetVideoMetadataCommand(Guid VideoId, VideoMetadataDto Metadata) : IRequest<Result>;
