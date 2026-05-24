// WebApi/Controllers/AnalyticsController.cs
using Application.Common;
using Application.CQRS.Analytics;
using Application.DTOs.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

using WebApi.Seeds;

namespace WebApi.Controllers;

/// <summary>
/// Manages publishing analytics, dashboard summaries, and manual metric sampling snapshots.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Analytics module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Provides key performance indicators (KPIs) and growth metrics to help brand owners track success across platforms.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Invokes MediatR CQRS commands and queries, querying databases and social clients, returning mapped analytics DTO structures.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals data isolation under multi-tenant environments. Ensures that users cannot scrape or query post analytics outside their active workspace.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class AnalyticsController : ApiControllerBase
{
    /// <summary>
    /// Collects a new analytics snapshot for a published post target manually.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/analytics/collect-snapshot - Target snapshot capture route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to sample immediate, up-to-date video performance (views, likes) from social platforms.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Takes a post target identifier, dispatches <see cref="CollectPostSnapshotCommand"/>, fetches provider metrics, and persists the record.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strict IDOR checks prevent users from sampling records outside their workspace scope. Custom rate-limiting protects integration channels from provider bans.</para>
    /// </remarks>
    /// <param name="postTargetId">The unique ID of the published post target.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("collect-snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CollectSnapshot([FromQuery] Guid postTargetId, CancellationToken ct)
    {
        var command = new CollectPostSnapshotCommand(postTargetId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves cross-platform analytics for a single post.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/analytics/post/{postId} - Cross-platform post metrics.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Provides unified metrics analysis of a video across YouTube Shorts, Instagram Reels, and TikTok.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Dispatches <see cref="GetPostAnalyticsQuery"/>, aggregates database results, and maps to analytics DTOs.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Leverages indexes for fast query resolution. Guarantees cross-tenant resource protection.</para>
    /// </remarks>
    /// <param name="postId">Post record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A cross-platform post analytics projection.</returns>
    [HttpGet("post/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostAnalyticsDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetPostAnalytics(Guid postId, CancellationToken ct)
    {
        var query = new GetPostAnalyticsQuery(postId);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the overall workspace dashboard analytics summary.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/analytics/dashboard - Workspace summary metrics.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Feeds high-level dashboard charts showing aggregate reach, engagement and subscriber trends.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Dispatches <see cref="GetDashboardSummaryQuery"/> with optional dates to fetch summary tables.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Enforces workspace boundaries. Caches intermediate aggregate values in Redis to avoid heavy database CPU utilization.</para>
    /// </remarks>
    /// <param name="from">Optional UTC date window start.</param>
    /// <param name="to">Optional UTC date window end.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Overall workspace dashboard statistics.</returns>
    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DashboardSummaryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetDashboardSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = new GetDashboardSummaryQuery(from, to);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}
