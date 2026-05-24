// WebApi/Controllers/NotificationController.cs
using Application.Common;
using Application.CQRS.Notifications;
using Application.DTOs.Notification;
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
/// Manages user notification channels, subscription preferences, and delivery history logs.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Notification module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Enables users to control how and when they receive system updates (e.g. Email on failed post, push on comments), optimizing engagement without message fatigue.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Coordinates updates to subscription states and lists historical triggers by dispatching MediatR actions linked to notifications persistence tables.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Locks privacy configurations. Ensures that notification preference updates are isolated strictly to the authenticated user's session claims.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class NotificationController : ApiControllerBase
{
    /// <summary>
    /// Retrieves the notification preference set for the current user in the active workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/notification/preferences - User preferences fetch.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Populates settings configuration checkboxes on the User Preferences dashboard panel.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries the preferences table filtering by current UserId and active workspace scope.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals data isolation under multi-tenant environments. Ensures cross-tenant privacy holds.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of notification preference states.</returns>
    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<NotificationPreferenceDto>))]
    public async Task<ActionResult> GetPreferences(CancellationToken ct)
    {
        var query = new GetNotificationPreferencesQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Creates or updates a single notification preference for the authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/notification/preferences - Preference mutation route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows toggle configuration for one specific channel and event category (e.g. disable email for success alerts).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Checks payload parameters, dispatches <see cref="UpdateNotificationPreferenceCommand"/>, and updates database records.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarantees identity mapping. Binds preferences strictly to the authenticated session context.</para>
    /// </remarks>
    /// <param name="request">Payload detailing the targeted event type, channel and enabled status.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated notification preference details representation.</returns>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NotificationPreferenceDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdatePreference([FromBody] UpdateNotificationPreferenceRequest request, CancellationToken ct)
    {
        var command = new UpdateNotificationPreferenceCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Replaces or updates the entire notification preference set for the authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/notification/preferences/bulk - Multi-preference update.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Processes bulk settings updates when the user clicks 'Save Settings' on their preferences view.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Loops over changes list, updates database state in a single SQL transaction, maps results.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Optimized transaction boundaries. Prevents partial preference updates on failure.</para>
    /// </remarks>
    /// <param name="request">Collection of preference configurations.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated notification preferences collection.</returns>
    [HttpPut("preferences/bulk")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<NotificationPreferenceDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> BulkUpdatePreferences([FromBody] BulkUpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        var command = new UpdateAllNotificationPreferencesCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves paginated notification delivery history for the authenticated user.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/notification/history - Delivery logs audit.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays a paginated log of previous notifications received by the user inside the notification drawer.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Filters notification logs by current user context claims and requested pagination limits.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents scraping logs of other platform members by strictly scoping database queries to active UserId.</para>
    /// </remarks>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of notification history events.</returns>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<NotificationHistoryDto>))]
    public async Task<ActionResult> GetHistory([FromQuery] int pageSize = 15, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetNotificationHistoryQuery(pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}
