// WebApi/Controllers/AutomationController.cs
using Application.Common;
using Application.CQRS.Automation;
using Application.DTOs.Automation;
using Application.DTOs.PendingDM;
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
/// Manages automated workflows, DM response rules, execution logs, and pending message queues.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Implements the Web API controllers for the Automation module.</para>
/// <para><b>Business &amp; Technical Justification:</b> Empowers creators to automate interactions, converting comments or DMs directly into traffic or sales via custom workflows.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Dispatches CQRS requests to retrieve rules, conditions, and pending queues, and triggers rule validation or background processing pipelines.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals critical system automation controls. Enforces strict tenant barriers to verify users can only read or edit automation rules matching their active workspace.</para>
/// </remarks>
[Authorize(Roles = $"{DefaultRoles.Administrator},{DefaultRoles.Manager},{DefaultRoles.User}")]
public sealed class AutomationController : ApiControllerBase
{
    /// <summary>
    /// Creates a new automation rule with conditions and actions.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/automation/rules - Rule creation endpoint.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Enables users to set up custom trigger-action pairs (e.g. reply with link when comment matches keyword).</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Validates conditions schema, maps entities, and saves the new graph structure.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Sanitizes triggers to avoid infinite response loops. Ensures tenant isolation during creation.</para>
    /// </remarks>
    /// <param name="request">Automation rule definition payload.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The newly created automation rule detail representation.</returns>
    [HttpPost("rules")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutomationRuleDetailDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CreateRule([FromBody] CreateAutomationRuleRequest request, CancellationToken ct)
    {
        var command = new CreateAutomationRuleCommand(request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Updates an existing automation rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> PUT /api/automation/rules/{id} - Rule updates route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to modify existing workflow keywords, templates, or thresholds.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Loads current graph from database, verifies ownership, updates parameters, and persists.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarded by IDOR constraints. Validates the rule ID against active workspace claims.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="request">Updated rule configuration properties.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The updated automation rule details.</returns>
    [HttpPut("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutomationRuleDetailDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateRule(Guid id, [FromBody] CreateAutomationRuleRequest request, CancellationToken ct)
    {
        var command = new UpdateAutomationRuleCommand(id, request);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Enables or disables an automation rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/automation/rules/{id}/toggle - State activation toggle.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows temporarily pausing an automation campaign without fully deleting the configuration.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Sets the Enabled flag, invalidates local cache registers, and updates DB.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seamless execution boundary. Quick response for operational state control.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="isEnabled">Desired rule status.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("rules/{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ToggleRule(Guid id, [FromQuery] bool isEnabled, CancellationToken ct)
    {
        var command = new ToggleAutomationRuleCommand(id, isEnabled);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes an automation rule and clears dependent configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> DELETE /api/automation/rules/{id} - Rule removal.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Cleans up obsolete automation campaigns and purging associated databases.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Verifies workspace limits, removes rule records, and cascades to logs and queues.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Critical admin function. Protects against cross-workspace deletions.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpDelete("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var command = new DeleteAutomationRuleCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a paginated list of automation rules for the active workspace.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/rules - Paginated rules list.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays configured automation rules in the primary workflows management UI.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries the database filtering by workspace, applying pagination, returning summaries.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Seals data isolation under multi-tenant environments.</para>
    /// </remarks>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of automation rules.</returns>
    [HttpGet("rules")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<AutomationRuleDto>))]
    public async Task<ActionResult> GetRules([FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetAutomationRulesQuery(pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves one automation rule with its complete conditions and actions configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/rules/{id} - Full rule load.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Required to populate the full workflow designer editor visual screen.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Loads rule, checks workspace matches, maps nested conditions/actions to DTO.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Defensive IDOR protection checks active workspace against matching records.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>The automation rule detailed projection payload.</returns>
    [HttpGet("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutomationRuleDetailDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetRuleById(Guid id, CancellationToken ct)
    {
        var query = new GetAutomationRuleByIdQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves condition types available in the system based on platform and trigger type.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/conditions - Available condition schemas query.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Populates UI dropdowns during rule setup based on context constraints.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries built-in capabilities lists matching the requested platform and trigger type.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents user from drafting rules with invalid platform-condition combinations.</para>
    /// </remarks>
    /// <param name="platform">The targeting social platform (e.g. YouTube, Instagram).</param>
    /// <param name="triggerType">The specific event trigger category (e.g. DM, Comment).</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A collection of supported conditions schemas.</returns>
    [HttpGet("conditions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<AvailableConditionDto>))]
    public async Task<ActionResult> GetAvailableConditions(
        [FromQuery] Platform platform,
        [FromQuery] AutomationTriggerType triggerType,
        CancellationToken ct)
    {
        var query = new GetAvailableConditionsQuery(platform, triggerType);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves paginated pending direct messages scheduled for delivery.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/pending-dm - Delivery queue monitor.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays queued DMs that are deferred to simulate natural typing and bypass provider anti-spam rules.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Queries the pending DM queue, filters by optional status and page size, returns list.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Real-time operational audit route for pending queue health monitoring.</para>
    /// </remarks>
    /// <param name="status">Optional status filter (e.g. Pending, Completed, Cancelled).</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of pending DMs.</returns>
    [HttpGet("pending-dm")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<PendingDMQueueDto>))]
    public async Task<ActionResult> GetPendingDMs([FromQuery] PendingDMStatus? status, [FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetPendingDMsQuery(status, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Processes all pending DMs eligible for immediate delivery.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/automation/pending-dm/process - Manual queue trigger.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows system administrators or support staff to manually force pending DM queue execution.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Collects eligible items, contacts social platform API endpoints, updates delivery statuses.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Leverages retry policies and circuit breakers to guarantee delivery under external errors.</para>
    /// </remarks>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("pending-dm/process")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ProcessPendingDMs(CancellationToken ct)
    {
        var command = new ProcessPendingDMsCommand();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancels a queued pending DM entry, preventing its delivery.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> POST /api/automation/pending-dm/{id}/cancel - Queue item revocation.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Allows users to block scheduled automated responses before the delay expires.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Marks the queue status as Cancelled, stops delivery tasks, and updates records.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Prevents unwanted automated message spamming when user intervenes manually.</para>
    /// </remarks>
    /// <param name="id">Pending DM queue entry identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Action success response.</returns>
    [HttpPost("pending-dm/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CancelPendingDM(Guid id, CancellationToken ct)
    {
        var command = new CancelPendingDMCommand(id);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves paginated automation execution logs for auditing.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/rules/{id}/execution-logs - Execution history query.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Crucial debugger board to diagnose why certain automation workflows were executed or skipped.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Filters execution tables by rule ID and optional parameters, returns page results.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Guarantees operational transparency. IDOR constraints ensure security boundaries remain sealed.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="outcome">Optional outcome filter (e.g. Triggered, Skipped, Failed).</param>
    /// <param name="from">Optional date range start filter.</param>
    /// <param name="to">Optional date range end filter.</param>
    /// <param name="pageSize">Size of the data page.</param>
    /// <param name="pageNumber">1-indexed page pointer.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>A paginated list of rule execution logs.</returns>
    [HttpGet("rules/{id:guid}/execution-logs")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<ExecutionLogDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetExecutionLogs(
        Guid id,
        [FromQuery] AutomationExecutionOutcome? outcome,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var pagination = new PagedRequest(pageNumber, pageSize);
        var query = new GetExecutionLogsQuery(id, outcome, from, to, pagination);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves effectiveness metrics and conversion statistics for a specific automation rule.
    /// </summary>
    /// <remarks>
    /// <para><b>Core Definition:</b> GET /api/automation/rules/{id}/effectiveness - Metrics analyzer route.</para>
    /// <para><b>Business &amp; Technical Justification:</b> Displays execution/conversion metrics (e.g. comments scanned vs replies sent vs link clicks) in UI rule cards.</para>
    /// <para><b>Execution, Process &amp; Relationships:</b> Aggregates execution log counters and engagement snapshots, compiling conversion percentages.</para>
    /// <para><b>Project Impact &amp; Indispensability:</b> Enables real-time marketing tracking. Caches results to prevent heavy query load on large rules.</para>
    /// </remarks>
    /// <param name="id">Automation rule identifier.</param>
    /// <param name="ct">Asynchronous cancellation token.</param>
    /// <returns>Statistics describing rule effectiveness.</returns>
    [HttpGet("rules/{id:guid}/effectiveness")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AutomationRuleEffectivenessDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetRuleEffectiveness(Guid id, CancellationToken ct)
    {
        var query = new GetRuleEffectivenessQuery(id);
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}
