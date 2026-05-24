// WebApi/Controllers/InboxController.cs
using Application.Common;
using Application.CQRS.Inbox;
using Application.DTOs.Inbox;
using Domain.Enums;
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
/// Manages the workspace messaging inbox, conversation thread workflows, agent assignments, and outbound replies.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Inbox module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Provides a unified social inbox for community managers to respond to DMs, comments, and mentions across platforms from a single interface.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Orchestrates thread status changes, support agent assignments, message reads, and MediatR reply triggers linked to social platform APIs.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals data partition boundaries. Protects sensitive customer chat logs from unauthorized or cross-tenant inspection.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class InboxController : ApiControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of conversations for the workspace inbox.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/conversations - Paginated inbox threads list.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Feeds the main messaging dashboard visual panel, showing ongoing dialogs and their triage states.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries conversation tables matching the active tenant workspace ID, applying custom filters, and returning summaries.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Implements defensive scope checks. Restricts threads display strictly to the current workspace scope.</para>
    /// </remarks>
    /// <param name="status">Filter by thread status (e.g. Open, Pending, Resolved).</param>
    /// <param name="platform">Filter by connected platform (e.g. Instagram).</param>
    /// <param name="assigneeId">Filter by assigned agent ID.</param>
    /// <param name="isUnread">Filter by unread messages presence.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of conversation summaries.</returns>
    [HttpGet("conversations")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<ConversationSummaryDto>))]
    public async Task<ActionResult> GetConversations(
        [FromQuery] ConversationStatus? status,
        [FromQuery] Platform? platform,
        [FromQuery] Guid? assigneeId,
        [FromQuery] bool? isUnread,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var filter = new InboxFilterRequest(platform?.ToString(), status?.ToString(), assigneeId, isUnread, null);
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetConversationsPagedQuery(filter, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves one conversation with its full message history and assignment details.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/conversations/{id} - Thread inspector.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Loads the active chat window, displaying previous chat text and assignments.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Dispatches <see cref="GetConversationDetailQuery"/>, checks session matches, returning detail projection.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents IDOR. Blocks reading threads belonging to external workspaces.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The complete conversation thread details payload.</returns>
    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ConversationDetailDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetConversationDetail(Guid id, CancellationToken ct)
    {
        var query = new GetConversationDetailQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the current unread conversation count.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/unread-count - Unread threads counter.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays a red badge or notification bubble in the UI navigation sidebar.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Aggregates conversation unread flags restricted by active workspace scope.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Caches count metrics in Redis to prevent massive database scan queries.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Count of unread threads.</returns>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    public async Task<ActionResult> GetUnreadCount(CancellationToken ct)
    {
        var query = new GetUnreadCountQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Searches inbox conversations using keywords.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/search - Search inbox threads.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables agents to find previous client interactions or messages using raw text search query.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Performs indexed text matching on message content tables filtered by current workspace.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Zero-leak multi-tenant search. Indexed queries bypass slow full table scans.</para>
    /// </remarks>
    /// <param name="queryText">Keyword text to search for.</param>
    /// <param name="status">Filter by thread status.</param>
    /// <param name="platform">Filter by social platform.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of search hits summaries.</returns>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<ConversationSearchResultDto>))]
    public async Task<ActionResult> SearchConversations(
        [FromQuery] string queryText,
        [FromQuery] ConversationStatus? status,
        [FromQuery] Platform? platform,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var filter = new InboxFilterRequest(platform?.ToString(), status?.ToString(), null, null, queryText);
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new SearchConversationsQuery(filter, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves paginated messages for a single conversation.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/conversations/{id}/messages - List messages in thread.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Handles infinite scrolling inside the chat window view, loading older messages as user scrolls up.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries the messages table filtering by thread, page, and workspace scope.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Restricts load size. Prevents out of memory allocations on large chat histories.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of message payloads.</returns>
    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<MessageDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetMessages(Guid id, [FromQuery] int pageSize = 20, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetMessagesPagedQuery(id, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves workload statistics across active agents in the workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/inbox/team-workload - Agent workload audit.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Helps administrators balance conversation triage queues across online support agents.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Counts open conversation assignments grouped by member ID within the tenant scope.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Promotes operational efficiency. Strict multi-tenant limits guarantee data privacy.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A list of agent workload balances.</returns>
    [HttpGet("team-workload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<TeamWorkloadDto>))]
    public async Task<ActionResult> GetTeamWorkload(CancellationToken ct)
    {
        var query = new GetTeamWorkloadQuery();
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Changes the triage status of an inbox conversation.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/inbox/conversations/{id}/status - Workflow triage modifier.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables agents to resolve, reopen, or hold conversations based on progress.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates state transitions, dispatches status updates, and fires real-time SignalR notifications.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals workflow integrity. Block invalid transitions using internal domain checks.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="status">The desired conversation status.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("conversations/{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ChangeStatus(Guid id, [FromQuery] ConversationStatus status, CancellationToken ct)
    {
        var command = new ChangeConversationStatusCommand(id, status);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Assigns a conversation to a workspace member.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/inbox/conversations/{id}/assign - Thread ownership route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Promotes team accountability by assigning explicit threads to agents.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Verifies membership rights, maps assignment schemas, updates thread entity properties.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents assigning threads to agents outside the matching tenant boundary.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="request">Payload containing target agent and optional assign note.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created assignment details.</returns>
    [HttpPost("conversations/{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ConversationAssignmentDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Assign(Guid id, [FromBody] AssignConversationRequest request, CancellationToken ct)
    {
        var command = new AssignConversationCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Marks all unread messages inside a conversation as read by the workspace team.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/inbox/conversations/{id}/read - Thread unread clear route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Marks threads as processed in workspace dashboards, updating global badges.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Clears unread flags, triggers DB updates, dispatches notification clears via hubs.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Essential for responsive collaboration views. Prevents badge state drift.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("conversations/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var command = new MarkConversationReadCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Sends an outbound reply message into the active conversation thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/inbox/conversations/{id}/messages - Thread reply post route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Sends direct responses to clients on Instagram, YouTube comments, etc., from the unified inbox UI.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates thread constraints, contacts platform API integration libraries, and appends the message to DB.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarantees identity logging. Logs agent UserId against message records to maintain security audit lines.</para>
    /// </remarks>
    /// <param name="id">Conversation thread identifier.</param>
    /// <param name="request">Outbound reply content payload.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The representation of the newly sent outbound message.</returns>
    [HttpPost("conversations/{id:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MessageDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SendReply(Guid id, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var command = new SendMessageCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes a locally stored message from a thread when operational regulations allow.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/inbox/messages/{messageId} - Local message removal.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows team admins to remove messages that violate policies or contain mistake uploads.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Verifies message ownership within current workspace, deletes database records, notifies clients.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Strictly guarded by IDOR checks. Prevents deleting chats from alternative accounts.</para>
    /// </remarks>
    /// <param name="messageId">Unique message record identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager}")]
    [HttpDelete("messages/{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteMessage(Guid messageId, CancellationToken ct)
    {
        var command = new DeleteMessageCommand(messageId);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}
