// WebApi/Controllers/VideoController.cs
using Application.Common;
using Application.CQRS.Videos;
using Application.DTOs.Post;
using Application.DTOs.Video;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApi.Controllers;

/// <summary>
/// Manages the video media library, chunky resumable uploads, technical processing (transcoding, normalization), and platform-specific format validation.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Videos module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Provides a robust media pipeline supporting large file uploads. Transcodes high-definition videos and generates thumbnails for automated publication.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Directs streaming chunk buffers, launches background ffmpeg transcoding workers, and validates video dimensions and bitrates against platform rules.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Employs zero-allocation streaming uploads. Strict IDOR rules isolate the media library per workspace to prevent cross-tenant media scraping.</para>
/// </remarks>
[Authorize]
public sealed class VideoController : ApiControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of videos from the workspace media library.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/video - Media library listing.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Feeds the video assets grid in the schedule post screen and media library page.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries workspace video catalog, applying status filters and standard pagination, returning summaries.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals data isolation under multi-tenant environments. Ensures cross-tenant leaks are impossible.</para>
    /// </remarks>
    /// <param name="status">Optional status filter for processing state.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of video summaries.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<VideoSummaryDto>))]
    public async Task<ActionResult> GetVideos([FromQuery] VideoStatus? status, [FromQuery] int pageSize = 12, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetVideosPagedQuery(status, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves one video with its technical metadata and playback information.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/video/{id} - Video asset details.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays media player, resolution, file size and processing logs in the asset inspector.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Loads video entity, verifies tenant permissions, maps to detail DTO.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Defensive IDOR validation. Blocks external tenants from accessing video profiles.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The complete video representation.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoDetailDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetVideoById(Guid id, CancellationToken ct)
    {
        var query = new GetVideoByIdQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Starts a resumable chunked upload session for a new video asset.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/video/upload/init - Resumable upload initialization.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Configures session records, validating name and total size before binary upload begins.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Saves upload request details, creates unique session ID, and returns upload limits.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Allocates storage limits proactively. Prevents DOS from anonymous files payload bombardment.</para>
    /// </remarks>
    /// <param name="request">Initial file parameters payload.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created upload session parameters details.</returns>
    [HttpPost("upload/init")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSessionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> InitUpload([FromBody] InitUploadRequest request, CancellationToken ct)
    {
        var command = new InitVideoUploadCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Uploads a binary chunk of a previously initialized video upload session.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/video/upload/{id}/chunks - Direct binary streaming chunk route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Streams files of several gigabytes chunk by chunk. Keeps server memory footprint negligible.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Streams request body directly downstream, appending to secure temp file path registers, verifying byte hashes.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Elite zero-allocation pipeline. Avoids massive server RAM overhead from buffering multipart requests.</para>
    /// </remarks>
    /// <param name="id">Upload session identifier.</param>
    /// <param name="chunkIndex">Zero-based chunk position index.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Chunk verification detail summary.</returns>
    [HttpPost("upload/{id:guid}/chunks")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChunkDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UploadChunk(Guid id, [FromQuery] int chunkIndex, CancellationToken ct)
    {
        var command = new UploadVideoChunkCommand(id, chunkIndex, Request.Body);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Finalizes an upload session and persists the uploaded video in the media library.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/video/upload/{id}/complete - Upload completion finalizer.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Assembles temp chunks, validates total byte sizes, and creates the primary Video entity.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Combines chunks, triggers anti-virus check, stores file on storage, updates status, and queues processing.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Secures file validity. Throws errors on corrupted assemblies or size mismatch.</para>
    /// </remarks>
    /// <param name="id">Upload session identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created video detail representation.</returns>
    [HttpPost("upload/{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoDetailDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CompleteUpload(Guid id, CancellationToken ct)
    {
        var command = new CompleteVideoUploadCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Triggers asynchronous video transcoding and processing.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/video/{id}/process - Processing trigger.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Re-queues processing if transcoding fails, or schedules post-upload optimization tasks.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Triggers background job registrar to spin up FFmpeg transcoders via Hangfire queues.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Runs asynchronously. Bypasses main HTTP request thread limits to protect server load.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/process")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Process(Guid id, CancellationToken ct)
    {
        var command = new ProcessVideoCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Soft-deletes a video asset from the media library.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/video/{id} - Video asset removal.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Removes unused assets from workspace galleries to release stored space quotas.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Flags database records as Deleted, schedules storage cleanup, stops processing jobs.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Tenant-bound scope checks prevent cross-workspace asset deletion.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteVideoCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Generates or refreshes a thumbnail for an uploaded video.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/video/{id}/thumbnail - Thumbnail generator.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Extracts image frame from video stream to render in post creation cards.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Commands FFmpeg thumbnail sampler, saves thumbnail image to storage, returns thumbnail URI.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Quick feedback. Protects against executing raw CLI scripts in HTTP threads.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly generated thumbnail URI string.</returns>
    [HttpPost("{id:guid}/thumbnail")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GenerateThumbnail(Guid id, CancellationToken ct)
    {
        var command = new GenerateVideoThumbnailCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Persists normalized technical metadata for a video.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/video/{id}/metadata - Metadata updater.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Updates technical attributes (width, height, codecs, duration) after successful FFprobe processing.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Saves parameters, updates technical profiles in DB, invalidates cached values.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Usually called internally by processing micro-workers. Fast database update paths.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="metadata">Normalized video metadata details.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPut("{id:guid}/metadata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SetMetadata(Guid id, [FromBody] VideoMetadataDto metadata, CancellationToken ct)
    {
        var command = new SetVideoMetadataCommand(id, metadata);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Validates whether a video is technically compatible with selected publishing platforms.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/video/{id}/validate - Platform compatibility check.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Provides proactive warning alerts (e.g. video too long for Shorts, invalid aspect ratio for Reels) before publishing is attempted.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Evaluates duration, dimensions and codec rules matching the requested platforms array.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Drastically lowers failed publishing attempt numbers by capturing formatting flaws early.</para>
    /// </remarks>
    /// <param name="id">Video record identifier.</param>
    /// <param name="platforms">Target publishing platforms array.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of platform validation results detailing warnings or errors.</returns>
    [HttpGet("{id:guid}/validate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<PlatformValidationDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ValidateForPlatforms(Guid id, [FromQuery] List<Platform> platforms, CancellationToken ct)
    {
        var query = new ValidateVideoForPlatformsQuery(id, platforms);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}
