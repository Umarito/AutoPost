// WebApi/Controllers/SocialAccountController.cs
using Application.Common;
using Application.CQRS.SocialAccounts;
using Application.DTOs.Analytics;
using Application.DTOs.SocialAccount;
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
/// Manages connection, authentication validity, metadata updates, and analytics for third-party social media platform accounts.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Social Account module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Bridges the platform with external publishers (YouTube, Instagram, etc.). Enables users to authenticate and review audience growth indicators.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Coordinates oauth requests, callback data storage, and analytics fetches via MediatR queries, linking to third-party clients.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Secures credential integration. Enforces CSRF matching on callbacks and validates tenant boundaries to prevent cross-account modifications.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class SocialAccountController : ApiControllerBase
{
    private readonly ICurrentUserContext _currentUserContext;

    /// <summary>
    /// Initializes the SocialAccountController.
    /// </summary>
    /// <param name="currentUserContext">Access to active workspace and user claims context.</param>
    public SocialAccountController(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    /// <summary>
    /// Generates the secure redirect authorization URL for a social platform connection flow.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/socialaccount/oauth-url - OAuth initialization endpoint.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Begins the secure OAuth handshake by fetching base provider URLs (Google, Meta dialogs).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Takes platform type and frontend redirect callback, queries client factory, and returns target uri.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals OAuth initiation state. Forces explicit callback path registration to protect against redirect URI hijacking.</para>
    /// </remarks>
    /// <param name="platform">The social platform (e.g., YouTube, Instagram).</param>
    /// <param name="redirectUri">Target frontend route which receives oauth code parameters.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The secure authorization URL details.</returns>
    [HttpGet("oauth-url")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OAuthUrlDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetOAuthUrl([FromQuery] Platform platform, [FromQuery] string redirectUri, CancellationToken ct)
    {
        var query = new GetOAuthUrlQuery(platform, redirectUri);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Completes the OAuth redirect callback flow and connects the social channel to the current workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/socialaccount/callback - Connection finalize endpoint.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Exchanges code parameters for long-lived access/refresh tokens and persists credentials securely.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Sends code parameters downstream, validates state cookies, exchanges tokens, encrypts credentials via DPAPI, and registers the account.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Encrypts tokens using DPAPI. Prevents database leaks of raw access tokens.</para>
    /// </remarks>
    /// <param name="request">Payload detailing the redirect callback code and states.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The connected social account information.</returns>
    [HttpPost("callback")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SocialAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> HandleCallback([FromBody] HandleCallbackRequest request, CancellationToken ct)
    {
        var command = new HandleOAuthCallbackCommand(
            request.Platform,
            request.AuthorizationCode,
            request.State,
            request.RedirectUri);

        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves all connected social accounts for the active workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/socialaccount - Connected accounts list.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Renders active connections and profiles for post scheduling UI selectors.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries accounts filtered by the active token's workspace ID claim.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals multi-tenant separation. Prevents cross-workspace data leakage.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of connected social account representations.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<SocialAccountDto>))]
    public async Task<ActionResult> GetConnected(CancellationToken ct)
    {
        var query = new GetSocialAccountsQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Disconnects and deletes a connected social account.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/socialaccount/{id} - Connection deletion.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows team administrators to disconnect accounts and purge credentials.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Requires workspace admin clearance, deletes credentials, and stops scheduled publish tasks.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarded by <c>WorkspaceAdmin</c> role policy. Prevents members from disconnecting key accounts.</para>
    /// </remarks>
    /// <param name="id">Connected social account identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Policy = "WorkspaceAdmin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Disconnect(Guid id, CancellationToken ct)
    {
        var command = new DisconnectSocialAccountCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Audits and validates the current OAuth token validity status for the channel.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/socialaccount/{id}/ensure-token - Verification route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows manual validation of token configurations if publishing fails.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Verifies token state, executes refreshes if expired, and updates database records.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Secures session lifetimes. Prevents publishing tasks from failing silently due to stale credentials.</para>
    /// </remarks>
    /// <param name="id">Connected social account identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/ensure-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> EnsureTokenValid(Guid id, CancellationToken ct)
    {
        var command = new EnsureTokenValidCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Forces a refresh of platform-cached channel metadata such as username or follower counts.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/socialaccount/{id}/refresh-meta - Metadata update route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Keeps profile statistics and visual avatars in sync with platform updates.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries live provider endpoints, grabs profile statistics, and updates the entity database.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Maximizes UI accuracy. Employs circuit breakers to avoid platform rate-limit penalties.</para>
    /// </remarks>
    /// <param name="id">Connected social account identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated social account details.</returns>
    [HttpPost("{id:guid}/refresh-meta")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SocialAccountDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RefreshMeta(Guid id, CancellationToken ct)
    {
        var command = new RefreshSocialAccountMetaCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Triggers a manual collection of account growth metrics and follower counts.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/socialaccount/{id}/collect-insight - Insight capture route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables users to trigger manual metric syncs without waiting for the daily background job.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Schedules a background task, contacts platform APIs, and pushes updates via SignalR.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Limits execution throttling via custom rate limit rules. Avoids concurrent database transaction lockouts.</para>
    /// </remarks>
    /// <param name="id">Connected social account identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("{id:guid}/collect-insight")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CollectInsight(Guid id, CancellationToken ct)
    {
        var command = new CollectAccountInsightCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves historical follower growth and account reach statistics.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/socialaccount/{id}/growth - Account analytics growth history.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Feeds the analytics dashboard to plot performance graphs.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries insights filtered by account ID and datetime windows, and projects to DTO list.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly filters queries using index parameters. Leverages Redis cache to prevent database load under heavy navigation.</para>
    /// </remarks>
    /// <param name="id">Connected social account identifier.</param>
    /// <param name="from">Inclusive start date of the analytics period.</param>
    /// <param name="to">Inclusive end date of the analytics period.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of growth insight snapshots.</returns>
    [HttpGet("{id:guid}/growth")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<InsightDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetGrowth(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var query = new GetAccountGrowthQuery(id, from, to);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}

/// <summary>
/// Defines requirements for completing the OAuth dialog callback.
/// </summary>
public record HandleCallbackRequest(Platform Platform, string AuthorizationCode, string State, string RedirectUri);
