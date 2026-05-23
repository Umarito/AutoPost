using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Caching;
using Application.Abstractions.Media;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Storage;
using Application.BackgroundJobs;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Post;
using Application.DTOs.Video;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Videos;

/// <summary>
/// Starts a Redis-backed resumable upload session for a video file inside the current workspace.
/// </summary>
public sealed class InitVideoUploadCommandHandler : IRequestHandler<InitVideoUploadCommand, Result<UploadSessionDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<InitVideoUploadCommandHandler> _logger;

    /// <summary>
    /// Initializes the upload-session bootstrap handler.
    /// </summary>
    public InitVideoUploadCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        ILogger<InitVideoUploadCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UploadSessionDto>> Handle(InitVideoUploadCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<UploadSessionDto>.Fail(access.Error!, access.Code!.Value);
            }

            var uploadId = Guid.NewGuid();
            const int chunkSizeBytes = 10 * 1024 * 1024;
            var totalChunks = (int)Math.Ceiling(request.Request.FileSizeBytes / (double)chunkSizeBytes);
            var state = new VideoUploadSessionState(
                uploadId,
                _currentUserContext.WorkspaceId,
                _currentUserContext.UserId,
                request.Request.FileName,
                request.Request.ContentType,
                request.Request.FileSizeBytes,
                chunkSizeBytes,
                totalChunks,
                []);

            await _cacheService.SetAsync(
                VideoWorkflow.BuildUploadStateKey(uploadId),
                state,
                TimeSpan.FromHours(6),
                cancellationToken);

            return Result<UploadSessionDto>.Ok(new UploadSessionDto(uploadId, chunkSizeBytes, totalChunks, "InProgress"));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while initializing a video upload session.");
            return Result<UploadSessionDto>.Fail("An unexpected upload-session initialization error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Uploads one chunk of a resumable video-upload session into blob storage.
/// </summary>
public sealed class UploadVideoChunkCommandHandler : IRequestHandler<UploadVideoChunkCommand, Result<ChunkDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICacheService _cacheService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<UploadVideoChunkCommandHandler> _logger;

    /// <summary>
    /// Initializes the chunk-upload handler.
    /// </summary>
    public UploadVideoChunkCommandHandler(
        ICurrentUserContext currentUserContext,
        ICacheService cacheService,
        IBlobStorageService blobStorageService,
        ILogger<UploadVideoChunkCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _cacheService = cacheService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ChunkDto>> Handle(UploadVideoChunkCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var state = await _cacheService.GetAsync<VideoUploadSessionState>(
                VideoWorkflow.BuildUploadStateKey(request.UploadId),
                cancellationToken);

            if (state is null)
            {
                return Result<ChunkDto>.Fail("Upload session was not found or has expired.", ErrorCode.NotFound);
            }

            if (state.WorkspaceId != _currentUserContext.WorkspaceId || state.UploadedByUserId != _currentUserContext.UserId)
            {
                return Result<ChunkDto>.Fail("Upload session ownership validation failed.", ErrorCode.Forbidden);
            }

            if (request.ChunkIndex < 0 || request.ChunkIndex >= state.TotalChunks)
            {
                return Result<ChunkDto>.Fail("Chunk index is outside the expected upload range.", ErrorCode.Validation);
            }

            var chunkBlobName = VideoWorkflow.BuildChunkBlobName(request.UploadId, request.ChunkIndex);
            await _blobStorageService.UploadAsync(
                VideoWorkflow.UploadsContainerName,
                chunkBlobName,
                request.Content,
                state.ContentType,
                cancellationToken);

            var uploadedChunks = state.UploadedChunks.Contains(request.ChunkIndex)
                ? state.UploadedChunks
                : state.UploadedChunks.Append(request.ChunkIndex).Distinct().Order().ToArray();

            var updated = state with { UploadedChunks = uploadedChunks };
            await _cacheService.SetAsync(
                VideoWorkflow.BuildUploadStateKey(request.UploadId),
                updated,
                TimeSpan.FromHours(6),
                cancellationToken);

            return Result<ChunkDto>.Ok(new ChunkDto(request.UploadId, request.ChunkIndex, uploadedChunks.Length, state.TotalChunks));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while uploading chunk {ChunkIndex} for upload session {UploadId}.", request.ChunkIndex, request.UploadId);
            return Result<ChunkDto>.Fail("An unexpected chunk upload error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Finalizes a video upload by concatenating chunk blobs, persisting the aggregate and scheduling processing.
/// </summary>
public sealed class CompleteVideoUploadCommandHandler : IRequestHandler<CompleteVideoUploadCommand, Result<VideoDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly ICacheService _cacheService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;
    private readonly IMapper _mapper;
    private readonly ILogger<CompleteVideoUploadCommandHandler> _logger;

    /// <summary>
    /// Initializes the upload-completion handler.
    /// </summary>
    public CompleteVideoUploadCommandHandler(
        ICurrentUserContext currentUserContext,
        IApplicationUserRepository applicationUserRepository,
        ICacheService cacheService,
        IBlobStorageService blobStorageService,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IBackgroundJobScheduler backgroundJobScheduler,
        IMapper mapper,
        ILogger<CompleteVideoUploadCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _applicationUserRepository = applicationUserRepository;
        _cacheService = cacheService;
        _blobStorageService = blobStorageService;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _backgroundJobScheduler = backgroundJobScheduler;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<VideoDetailDto>> Handle(CompleteVideoUploadCommand request, CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{request.UploadId}.upload");

        try
        {
            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                _currentUserContext.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<VideoDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            var state = await _cacheService.GetAsync<VideoUploadSessionState>(
                VideoWorkflow.BuildUploadStateKey(request.UploadId),
                cancellationToken);

            if (state is null)
            {
                return Result<VideoDetailDto>.Fail("Upload session was not found or has expired.", ErrorCode.NotFound);
            }

            if (state.WorkspaceId != _currentUserContext.WorkspaceId || state.UploadedByUserId != _currentUserContext.UserId)
            {
                return Result<VideoDetailDto>.Fail("Upload session ownership validation failed.", ErrorCode.Forbidden);
            }

            if (state.UploadedChunks.Length != state.TotalChunks)
            {
                return Result<VideoDetailDto>.Fail("The upload session is incomplete and cannot be finalized yet.", ErrorCode.Validation);
            }

            var finalVideoId = Guid.NewGuid();
            var extension = Path.GetExtension(state.FileName);
            var finalStorageKey = $"{state.WorkspaceId}/{finalVideoId}{extension}";

            await using (var tempFile = File.Create(tempFilePath))
            {
                foreach (var chunkIndex in Enumerable.Range(0, state.TotalChunks))
                {
                    await using var chunkStream = await _blobStorageService.OpenReadAsync(
                        VideoWorkflow.UploadsContainerName,
                        VideoWorkflow.BuildChunkBlobName(request.UploadId, chunkIndex),
                        cancellationToken);

                    await chunkStream.CopyToAsync(tempFile, cancellationToken);
                }
            }

            await using (var finalContent = File.OpenRead(tempFilePath))
            {
                await _blobStorageService.UploadAsync(
                    VideoWorkflow.VideosContainerName,
                    finalStorageKey,
                    finalContent,
                    state.ContentType,
                    cancellationToken);
            }

            var uploadedBy = await _applicationUserRepository.GetByIdAsync(_currentUserContext.UserId, cancellationToken);
            if (uploadedBy is null)
            {
                return Result<VideoDetailDto>.Fail("Current user was not found.", ErrorCode.Unauthorized);
            }

            var video = Video.Create(
                state.WorkspaceId,
                state.UploadedByUserId,
                finalStorageKey,
                _blobStorageService.GetBlobUri(VideoWorkflow.VideosContainerName, finalStorageKey).ToString(),
                state.FileName,
                state.ContentType,
                state.FileSizeBytes,
                DateTime.UtcNow);

            video.MarkProcessing();

            await _videoRepository.AddAsync(video, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var chunkIndex in state.UploadedChunks)
            {
                await _blobStorageService.DeleteAsync(
                    VideoWorkflow.UploadsContainerName,
                    VideoWorkflow.BuildChunkBlobName(request.UploadId, chunkIndex),
                    cancellationToken);
            }

            await _cacheService.RemoveAsync(VideoWorkflow.BuildUploadStateKey(request.UploadId), cancellationToken);
            await VideoWorkflow.InvalidateReadCachesAsync(_cacheService, state.WorkspaceId, video.Id, cancellationToken);

            _backgroundJobScheduler.Enqueue<ContentBackgroundJobDispatcher>(
                dispatcher => dispatcher.ProcessVideoAsync(video.Id),
                queue: "default");

            var dto = new VideoDetailDto(
                video.Id,
                video.OriginalFileName,
                video.CdnUrl,
                video.ThumbnailUrl,
                video.FileSizeBytes,
                video.Status.ToString(),
                null,
                video.UploadedAt,
                uploadedBy.DisplayName);

            return Result<VideoDetailDto>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while completing upload session {UploadId}.", request.UploadId);
            return Result<VideoDetailDto>.Fail("An unexpected upload-finalization error occurred.", ErrorCode.Unknown);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}

/// <summary>
/// Executes the durable video-processing workflow that enriches metadata and preview assets.
/// </summary>
public sealed class ProcessVideoCommandHandler : IRequestHandler<ProcessVideoCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProcessVideoCommandHandler> _logger;

    /// <summary>
    /// Initializes the video-processing handler.
    /// </summary>
    public ProcessVideoCommandHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IVideoProcessingService videoProcessingService,
        ICacheService cacheService,
        ILogger<ProcessVideoCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _videoProcessingService = videoProcessingService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(ProcessVideoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoRepository.GetByIdIncludingDeletedAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return ContentGuard.NotFound("Video");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    video.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            await _videoProcessingService.ProcessAsync(request.VideoId, cancellationToken);
            await VideoWorkflow.InvalidateReadCachesAsync(_cacheService, video.WorkspaceId, video.Id, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while processing video {VideoId}.", request.VideoId);
            return Result.Fail("An unexpected video-processing error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Soft-deletes a video after validating workspace ownership and content-write permissions.
/// </summary>
public sealed class DeleteVideoCommandHandler : IRequestHandler<DeleteVideoCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IPostRepository _postRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeleteVideoCommandHandler> _logger;

    /// <summary>
    /// Initializes the video-delete handler.
    /// </summary>
    public DeleteVideoCommandHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IPostRepository postRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<DeleteVideoCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _postRepository = postRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(DeleteVideoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return ContentGuard.NotFound("Video");
            }

            var access = await ContentGuard.RequireContentWriteAccessAsync(
                _currentUserContext.UserId,
                video.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result.Fail(access.Error!, access.Code!.Value);
            }

            var workspacePosts = await _postRepository.GetByWorkspaceIdAsync(video.WorkspaceId, ct: cancellationToken);
            var hasLiveDependencies = workspacePosts.Any(post =>
                post.VideoId == video.Id &&
                post.Status is not PostStatus.Cancelled and not PostStatus.Failed);

            if (hasLiveDependencies)
            {
                return Result.Fail("The video is still referenced by active or scheduled posts.", ErrorCode.Conflict);
            }

            video.SoftDelete(DateTime.UtcNow);
            _videoRepository.Update(video);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await VideoWorkflow.InvalidateReadCachesAsync(_cacheService, video.WorkspaceId, video.Id, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while deleting video {VideoId}.", request.VideoId);
            return Result.Fail("An unexpected video-deletion error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns the paged media library for the current workspace and optionally filters by processing status.
/// </summary>
public sealed class GetVideosPagedQueryHandler : IRequestHandler<GetVideosPagedQuery, Result<PagedResult<VideoSummaryDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetVideosPagedQueryHandler> _logger;

    /// <summary>
    /// Initializes the paged-video query handler.
    /// </summary>
    public GetVideosPagedQueryHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetVideosPagedQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<VideoSummaryDto>>> Handle(GetVideosPagedQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserContext.WorkspaceId;
        var cacheKey = VideoWorkflow.BuildVideosCacheKey(workspaceId, request.Status, request.Pagination);

        try
        {
            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                workspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<PagedResult<VideoSummaryDto>>.Fail(access.Error!, access.Code!.Value);
            }

            var cached = await _cacheService.GetAsync<PagedResult<VideoSummaryDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<PagedResult<VideoSummaryDto>>.Ok(cached);
            }

            var videos = await _videoRepository.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
            var filtered = request.Status.HasValue
                ? videos.Where(video => video.Status == request.Status.Value).ToList()
                : videos;

            var page = Math.Max(1, request.Pagination.Page);
            var pageSize = Math.Max(1, request.Pagination.PageSize);
            var pagedItems = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(_mapper.Map<VideoSummaryDto>)
                .ToArray();

            var result = new PagedResult<VideoSummaryDto>(pagedItems, filtered.Count, page, pageSize);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<PagedResult<VideoSummaryDto>>.Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading videos for workspace {WorkspaceId}.", workspaceId);
            return Result<PagedResult<VideoSummaryDto>>.Fail("An unexpected video-library lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Returns one video detail model after validating workspace ownership against the database.
/// </summary>
public sealed class GetVideoByIdQueryHandler : IRequestHandler<GetVideoByIdQuery, Result<VideoDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IApplicationUserRepository _applicationUserRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<GetVideoByIdQueryHandler> _logger;

    /// <summary>
    /// Initializes the single-video query handler.
    /// </summary>
    public GetVideoByIdQueryHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IApplicationUserRepository applicationUserRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<GetVideoByIdQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _applicationUserRepository = applicationUserRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<VideoDetailDto>> Handle(GetVideoByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = VideoWorkflow.BuildVideoCacheKey(request.VideoId);

        try
        {
            var cached = await _cacheService.GetAsync<VideoDetailDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                var access = await ContentGuard.RequireReadAccessAsync(
                    _currentUserContext.UserId,
                    _currentUserContext.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                return access.IsSuccess
                    ? Result<VideoDetailDto>.Ok(cached)
                    : Result<VideoDetailDto>.Fail(access.Error!, access.Code!.Value);
            }

            var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return ContentGuard.NotFound<VideoDetailDto>("Video");
            }

            var accessToWorkspace = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                video.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!accessToWorkspace.IsSuccess)
            {
                return Result<VideoDetailDto>.Fail(accessToWorkspace.Error!, accessToWorkspace.Code!.Value);
            }

            var uploadedBy = await _applicationUserRepository.GetByIdAsync(video.UploadedByUserId, cancellationToken);
            var dto = new VideoDetailDto(
                video.Id,
                video.OriginalFileName,
                video.CdnUrl,
                video.ThumbnailUrl,
                video.FileSizeBytes,
                video.Status.ToString(),
                video.Metadata is null ? null : _mapper.Map<VideoMetadataDto>(video.Metadata),
                video.UploadedAt,
                uploadedBy?.DisplayName ?? "Unknown user");

            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return Result<VideoDetailDto>.Ok(dto);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while loading video {VideoId}.", request.VideoId);
            return Result<VideoDetailDto>.Fail("An unexpected video lookup error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Validates one uploaded video against platform-specific compatibility rules before scheduling publications.
/// </summary>
public sealed class ValidateVideoForPlatformsQueryHandler : IRequestHandler<ValidateVideoForPlatformsQuery, Result<IReadOnlyList<PlatformValidationDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly ILogger<ValidateVideoForPlatformsQueryHandler> _logger;

    /// <summary>
    /// Initializes the platform-validation query handler.
    /// </summary>
    public ValidateVideoForPlatformsQueryHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        ILogger<ValidateVideoForPlatformsQueryHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PlatformValidationDto>>> Handle(ValidateVideoForPlatformsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return Result<IReadOnlyList<PlatformValidationDto>>.Fail("Video was not found.", ErrorCode.NotFound);
            }

            var access = await ContentGuard.RequireReadAccessAsync(
                _currentUserContext.UserId,
                video.WorkspaceId,
                _workspaceMemberRepository,
                cancellationToken);

            if (!access.IsSuccess)
            {
                return Result<IReadOnlyList<PlatformValidationDto>>.Fail(access.Error!, access.Code!.Value);
            }

            if (video.Metadata is null)
            {
                return Result<IReadOnlyList<PlatformValidationDto>>.Fail("Video metadata is not available yet.", ErrorCode.Conflict);
            }

            var validations = request.Platforms
                .Distinct()
                .Select(platform => VideoWorkflow.ValidateForPlatform(platform, video.Metadata))
                .ToArray();

            return Result<IReadOnlyList<PlatformValidationDto>>.Ok(validations);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while validating video {VideoId} for platforms.", request.VideoId);
            return Result<IReadOnlyList<PlatformValidationDto>>.Fail("An unexpected platform-validation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Generates or refreshes a preview thumbnail for a video and invalidates dependent caches.
/// </summary>
public sealed class GenerateVideoThumbnailCommandHandler : IRequestHandler<GenerateVideoThumbnailCommand, Result<string>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GenerateVideoThumbnailCommandHandler> _logger;

    /// <summary>
    /// Initializes the thumbnail-generation handler.
    /// </summary>
    public GenerateVideoThumbnailCommandHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IVideoProcessingService videoProcessingService,
        ICacheService cacheService,
        ILogger<GenerateVideoThumbnailCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _videoProcessingService = videoProcessingService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> Handle(GenerateVideoThumbnailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return ContentGuard.NotFound<string>("Video");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    video.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result<string>.Fail(access.Error!, access.Code!.Value);
                }
            }

            var thumbnailUrl = await _videoProcessingService.GenerateThumbnailAsync(request.VideoId, cancellationToken);
            await VideoWorkflow.InvalidateReadCachesAsync(_cacheService, video.WorkspaceId, video.Id, cancellationToken);
            return Result<string>.Ok(thumbnailUrl);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while generating thumbnail for video {VideoId}.", request.VideoId);
            return Result<string>.Fail("An unexpected thumbnail-generation error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Persists normalized technical metadata that was extracted asynchronously from an uploaded video.
/// </summary>
public sealed class SetVideoMetadataCommandHandler : IRequestHandler<SetVideoMetadataCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IVideoRepository _videoRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SetVideoMetadataCommandHandler> _logger;

    /// <summary>
    /// Initializes the metadata-persistence handler.
    /// </summary>
    public SetVideoMetadataCommandHandler(
        ICurrentUserContext currentUserContext,
        IVideoRepository videoRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<SetVideoMetadataCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _videoRepository = videoRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(SetVideoMetadataCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoRepository.GetByIdIncludingDeletedAsync(request.VideoId, cancellationToken);
            if (video is null)
            {
                return ContentGuard.NotFound("Video");
            }

            if (_currentUserContext.UserId != Guid.Empty)
            {
                var access = await ContentGuard.RequireContentWriteAccessAsync(
                    _currentUserContext.UserId,
                    video.WorkspaceId,
                    _workspaceMemberRepository,
                    cancellationToken);

                if (!access.IsSuccess)
                {
                    return Result.Fail(access.Error!, access.Code!.Value);
                }
            }

            video.SetMetadata(VideoMetadata.Create(
                request.Metadata.DurationSeconds,
                request.Metadata.Width,
                request.Metadata.Height,
                request.Metadata.AspectRatio,
                request.Metadata.FrameRate,
                request.Metadata.VideoCodec,
                request.Metadata.AudioCodec,
                0,
                request.Metadata.HasAudio));

            _videoRepository.Update(video);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await VideoWorkflow.InvalidateReadCachesAsync(_cacheService, video.WorkspaceId, video.Id, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure while setting metadata for video {VideoId}.", request.VideoId);
            return Result.Fail("An unexpected metadata persistence error occurred.", ErrorCode.Unknown);
        }
    }
}

/// <summary>
/// Redis-backed resumable upload session state used by the video-upload pipeline.
/// </summary>
internal sealed record VideoUploadSessionState(
    Guid UploadId,
    Guid WorkspaceId,
    Guid UploadedByUserId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    int ChunkSizeBytes,
    int TotalChunks,
    int[] UploadedChunks);

/// <summary>
/// Shared helper methods for video handlers.
/// </summary>
internal static class VideoWorkflow
{
    internal const string UploadsContainerName = "video-uploads";
    internal const string VideosContainerName = "videos";

    internal static string BuildUploadStateKey(Guid uploadId) => $"video-upload:{uploadId}";

    internal static string BuildChunkBlobName(Guid uploadId, int chunkIndex) => $"{uploadId}/chunks/{chunkIndex:D5}.part";

    internal static string BuildVideosCacheKey(Guid workspaceId, VideoStatus? status, PagedRequest pagination)
        => $"videos:{workspaceId}:{status?.ToString() ?? "all"}:{pagination.Page}:{pagination.PageSize}";

    internal static string BuildVideoCacheKey(Guid videoId) => $"video:{videoId}";

    internal static async Task InvalidateReadCachesAsync(ICacheService cacheService, Guid workspaceId, Guid videoId, CancellationToken ct)
    {
        await cacheService.RemoveAsync(BuildVideoCacheKey(videoId), ct);
    }

    internal static PlatformValidationDto ValidateForPlatform(Platform platform, VideoMetadata metadata)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        switch (platform)
        {
            case Platform.Instagram:
                if (metadata.DurationSeconds > 90)
                {
                    warnings.Add("Instagram Reels usually performs best below 90 seconds.");
                }

                if (metadata.Height <= metadata.Width)
                {
                    warnings.Add("Vertical 9:16 media is recommended for Instagram Reels.");
                }

                break;
            case Platform.TikTok:
                if (metadata.DurationSeconds > 600)
                {
                    errors.Add("TikTok uploads must not exceed 10 minutes in the current MVP validation profile.");
                }

                if (metadata.Height <= metadata.Width)
                {
                    warnings.Add("Vertical 9:16 media is recommended for TikTok.");
                }

                break;
            case Platform.YouTube:
                if (metadata.DurationSeconds <= 0)
                {
                    errors.Add("YouTube requires a valid media duration.");
                }

                break;
            case Platform.Facebook:
            case Platform.Twitter:
            case Platform.Telegram:
                if (metadata.DurationSeconds <= 0)
                {
                    errors.Add("A valid media duration is required.");
                }

                break;
        }

        if (metadata.Width < 360 || metadata.Height < 360)
        {
            errors.Add("Video resolution is below the minimum supported baseline of 360px.");
        }

        if (string.IsNullOrWhiteSpace(metadata.VideoCodec))
        {
            errors.Add("Video codec information is unavailable.");
        }

        return new PlatformValidationDto(platform.ToString(), errors.Count == 0, warnings, errors);
    }
}
