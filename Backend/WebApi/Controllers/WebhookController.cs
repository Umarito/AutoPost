// WebApi/Controllers/WebhookController.cs
using Application.Common;
using Application.CQRS.Webhooks;
using Application.DTOs.Webhook;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WebApi.Controllers;

/// <summary>
/// Manages third-party platform webhook integrations, handshake verification checks, real-time message routing, and operational retry loops.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Webhook module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Essential for real-time operations. Receives asynchronous comments or message events from Meta, YouTube, or TikTok without polling pipelines.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Captures raw payloads, confirms signature verification hashes, stores events for background processing, and validates subscription handshakes.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> High scalability entry point. Uses anonymous, fast write-to-database queues to prevent request timeouts. Restricts administration views to authorized members.</para>
/// </remarks>
[Authorize]
public sealed class WebhookController : ApiControllerBase
{
    /// <summary>
    /// Validates an external provider's subscription challenge request (e.g. Meta Graph handshake).
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/webhook/{platform} - Subscription handshake endpoint.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Verifies webhook receiver endpoints exist and comply with platform security standards.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Echoes back the challenge parameter strictly when validation tokens match local configurations.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Exposed publicly. Requires plain text response mapping for compatibility.</para>
    /// </remarks>
    /// <param name="platform">The targeting social platform name.</param>
    /// <param name="challenge">The random string challenge parameter issued by the platform.</param>
    /// <param name="token">Optional verification token used to authenticate the provider.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The raw challenge string payload on success.</returns>
    [AllowAnonymous]
    [HttpGet("{platform}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> VerifySubscription(
        string platform,
        [FromQuery] string challenge,
        [FromQuery] string? token,
        CancellationToken ct)
    {
        var query = new VerifyWebhookSubscriptionQuery(platform, challenge, token);
        var result = await Mediator.Send(query, ct);
        if (result.IsSuccess)
        {
            return Content(result.Value ?? "", "text/plain");
        }
        return HandleResult(result);
    }

    /// <summary>
    /// Persists a received third-party webhook event for asynchronous background processing.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/webhook/{platform} - Webhook receiver.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Receives real-time triggers (like new direct message or post comment) and immediately stores them.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Reads the raw payload body, fetches signatures and event types from headers, and logs event records.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> High throughput, low latency boundary. Prevents long-running computations from timing out the platform's HTTP thread.</para>
    /// </remarks>
    /// <param name="platform">Source platform name (e.g. Meta, YouTube).</param>
    /// <param name="eventType">Optional event type header value.</param>
    /// <param name="signature">Optional signature header value used for authenticity checks.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The created webhook event tracking ID.</returns>
    [AllowAnonymous]
    [HttpPost("{platform}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ReceiveWebhook(
        string platform,
        [FromHeader(Name = "X-Event-Type")] string? eventType,
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature,
        CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(ct);

        var finalEventType = eventType ?? Request.Headers["X-Event-Type"].ToString();
        if (string.IsNullOrWhiteSpace(finalEventType))
        {
            finalEventType = "GenericEvent";
        }

        var finalSignature = signature ?? Request.Headers["X-Hub-Signature-256"].ToString();
        if (string.IsNullOrWhiteSpace(finalSignature))
        {
            finalSignature = Request.Headers["X-Signature"].ToString();
        }

        var command = new ReceiveWebhookCommand(platform, finalEventType, rawPayload, finalSignature);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves paginated webhook events for monitoring and operational visibility.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/webhook - Webhook monitoring board.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables system administrators to inspect, filter, and track real-time delivery performance.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries webhook event logs applying status, platform, and date filters.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Protected by Admin authorization limits. Prevents tenant members from accessing system event registries.</para>
    /// </remarks>
    /// <param name="status">Optional status filter (e.g. Received, Processed, Failed).</param>
    /// <param name="platform">Optional social platform filter.</param>
    /// <param name="from">Optional date range start boundary.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of webhook event summaries.</returns>
    [HttpGet]
    [Authorize(Policy = "WorkspaceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<WebhookEventDto>))]
    public async Task<ActionResult> GetEvents(
        [FromQuery] WebhookEventStatus? status,
        [FromQuery] Platform? platform,
        [FromQuery] DateTime? from,
        [FromQuery] int pageSize = 20,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetWebhookEventsQuery(status, platform, from, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Manually triggers processing for a previously stored webhook event.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/webhook/{id}/process - Event processing route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows forced processing of events that got stuck in queue or require immediate trigger.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Grabs webhook record details, runs parsing, matches rule criteria, routes messaging updates.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Admin-level operational task. Runs within Hangfire retry contexts.</para>
    /// </remarks>
    /// <param name="id">Webhook event identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/process")]
    [Authorize(Policy = "WorkspaceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Process(Guid id, CancellationToken ct)
    {
        var command = new ProcessWebhookCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Re-runs processing for a webhook event that previously failed or needs replay.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/webhook/{id}/reprocess - Event reprocessor.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables support or operational teams to replay historical events after bugs are fixed.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Resets the status to Received and triggers processing pipelines.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restores transaction safety. Prevents permanent event drops under unexpected bugs.</para>
    /// </remarks>
    /// <param name="id">Webhook event identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = "WorkspaceAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Reprocess(Guid id, CancellationToken ct)
    {
        var command = new ReprocessWebhookEventCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}
