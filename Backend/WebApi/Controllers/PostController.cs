// WebApi/Controllers/PostController.cs
using Application.Common;
using Application.CQRS.Posts;
using Application.DTOs.Post;
using Application.DTOs.PublishingJob;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using WebApi.Seeds;

namespace WebApi.Controllers;

/// <summary>
/// Handles creation, rescheduling, immediate publication, and retrieval of multi-platform post entities within a tenant boundary.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements Web API controller actions for the Post module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Core user publisher flow. Enables scheduling short-form videos and tracking their publishing states.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Maps inbound HTTP requests to MediatR commands, checking parameters, and dispatching to Hangfire queues.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Restricts cross-tenant access. Confirms user scope context on every update/reschedule command to eliminate IDOR.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class PostController : ApiControllerBase
{
    private readonly ICurrentUserContext _currentUserContext;

    /// <summary>
    /// Initializes the PostController.
    /// </summary>
    /// <param name="currentUserContext">Access to current user identity and workspace claims.</param>
    public PostController(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    /// <summary>
    /// Creates a new scheduled post with its platform publishing targets.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/post - Creation route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Primary path to compose and queue short videos for publishing targets.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Parses parameters, dispatches <see cref="CreatePostCommand"/>, builds platform configs, and queues tasks in Hangfire.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Limits body sizes to prevent DOS attacks. Binds targets to checked workspace social accounts.</para>
    /// </remarks>
    /// <param name="request">Payload mapping video links, captions, scheduled time, and target account IDs.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The detailed metadata of the newly created post.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDetailDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create([FromBody] CreatePostRequest request, CancellationToken ct)
    {
        var command = new CreatePostCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates properties of a draft or scheduled post before publishing starts.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/post/{id} - Post updates.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables users to fix captions, tags, or visibility options before live distribution.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Pulls current state, verifies status is Draft/Scheduled, modifies fields, and updates Hangfire job registrations.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Denies mutations to published posts. Enforces workspace boundary matches to block IDOR.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="request">Payload containing properties to update.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated post details representation.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager}")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDetailDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePostRequest request, CancellationToken ct)
    {
        var command = new UpdatePostCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancels a scheduled post and revokes its active Hangfire background job.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/post/{id}/cancel - Schedule cancellation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables users to halt upcoming publications in case of content errors.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Checks post state, deletes linked Hangfire scheduling token, and marks post as Cancelled/Draft.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents orphan worker execution. Validates tenant context.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var command = new CancelPostCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Permanently deletes a draft or cancelled post.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/post/{id} - Post deletion.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Purges unwanted posts and clears associated storage media.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Verifies deletion rules, clears database records, and triggers media storage cleanup tasks.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly restricts deletion to valid tenant records via context checks.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager}")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeletePostCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Reschedules the publication time of an existing scheduled post.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/post/{id}/reschedule - Publishing reschedule.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to adjust content calendar time slots dynamically.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Confirms post state, schedules the new Hangfire execution token, and logs time changes.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Forces IANA timezone checks to ensure scheduling logic preserves daylight savings context.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="request">Payload containing the new planned publication time and local timezone ID.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated post details representation.</returns>
    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDetailDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Reschedule(Guid id, [FromBody] ReschedulePostRequest request, CancellationToken ct)
    {
        var command = new ReschedulePostCommand(id, request.ScheduledAt, request.TimeZoneId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Triggers immediate publication of a scheduled or draft post, bypasses the calendar delay.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/post/{id}/publish - Immediate publish command.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables hot-fixes or breaking news releases to publish instantly.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Cancels active scheduled jobs and schedules immediate execution воркеров in Hangfire.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Bypasses delay checks while preserving full audit trial and webhook callback tracking.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Publish(Guid id, CancellationToken ct)
    {
        var command = new PublishPostCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a paginated list of posts matching status, platform, and date-range filters.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post - Filtered list of tenant posts.</para>
    /// <para><b>Business &amp; Technical Justification:</b> populates search, history grids, and dashboard content management systems.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Resolves filters, reads cache stamp, checks Redis, and retrieves database records if cache misses.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Optimized throughput via Redis caching. Zero cross-workspace leaks via claims isolation.</para>
    /// </remarks>
    /// <param name="status">Filter by post lifecycle status (e.g. Scheduled, Failed).</param>
    /// <param name="platform">Filter by connected platform (e.g. Instagram).</param>
    /// <param name="from">Optional UTC date window start.</param>
    /// <param name="to">Optional UTC date window end.</param>
    /// <param name="search">Text query matching title content.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of post summaries.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<PostSummaryDto>))]
    public async Task<ActionResult> GetPaged(
        [FromQuery] string? status,
        [FromQuery] string? platform,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var filter = new PostFilterRequest(status, platform, from, to, search);
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetPostsPagedQuery(filter, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves full detailed projection of a post, including targets and error tracking logs.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post/{id} - Detailed post profile query.</para>
    /// <para><b>Business &amp; Technical Justification:</b> populates post detail inspectors, error review screens, and retry prompts.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries repository, loads targets, fetches video metadata and maps output details.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Enforces strict IDOR boundaries, blocking external workspace queries.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The full post detailed payload.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDetailDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetById(Guid id, CancellationToken ct)
    {
        var query = new GetPostByIdQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves lightweight post objects optimized for rendering calendar visual events.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post/calendar - Calendar events fetch.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Feeds the Interactive Content Calendar UI grid.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries database using narrow start/end dates. Bypasses description loads for performance.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restricts loading size. Leverages index ranges to avoid table scan queries.</para>
    /// </remarks>
    /// <param name="from">Inclusive start of the calendar window.</param>
    /// <param name="to">Inclusive end of the calendar window.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of calendar post events.</returns>
    [HttpGet("calendar")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<PostCalendarDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetCalendar([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var query = new GetPostCalendarQuery(from, to);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Aggregates publishing performance statistics inside a selected time period.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post/statistics - Publishing KPIs query.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Renders KPI cards (total scheduled, success rate, failures count) on the main workspace page.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> aggregates totals, calculates success ratios, and projects results.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Boosts analytics load speeds using optimized db count scripts.</para>
    /// </remarks>
    /// <param name="from">Optional start date boundary.</param>
    /// <param name="to">Optional end date boundary.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>KPI publishing metrics.</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostStatisticsDto))]
    public async Task<ActionResult> GetStatistics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = new GetPostsStatisticsQuery(from, to);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists historical execution log parameters for all publish attempts of a post.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post/{id}/publishing-history - Audit logs query.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Shows detailed diagnostic runs, API error codes, and provider responses for failed posts.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries the execution history tables, paginates results, and returns diagnostic records.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Important security audit reference. Allows developers to diagnostic connection drops without exposing DB credentials.</para>
    /// </remarks>
    /// <param name="id">Post record identifier.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of publishing job history details.</returns>
    [HttpGet("{id:guid}/publishing-history")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<PublishingJobDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetPublishingHistory(Guid id, [FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetPublishingHistoryQuery(id, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Lists all recent failed publishing attempts within the active workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/post/failed-publications - Operational failure tracker.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Consolidates all system failures into a single list for admins to review and retry.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Filters execution tables for failed states within the current workspace scope.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Crucial tool for operational maintenance. Avoids individual post polling overhead.</para>
    /// </remarks>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of failed publishing job summaries.</returns>
    [HttpGet("failed-publications")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<PublishingJobDto>))]
    public async Task<ActionResult> GetFailedPublications([FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetFailedPublicationsQuery(pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}

/// <summary>
/// Defines rescheduling requirements for a post.
/// </summary>
public record ReschedulePostRequest(DateTime ScheduledAt, string TimeZoneId);
