using System.Text.Json;
using Application.Abstractions.Integrations;
using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Webhooks;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Automation;
using Application.DTOs.PendingDM;
using Application.DTOs.SocialAccount;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Automation;

/// <summary>
/// Handles rule creation. Verifies subscription limits and user permission.
/// </summary>
public sealed class CreateAutomationRuleCommandHandler
    : IRequestHandler<CreateAutomationRuleCommand, Result<AutomationRuleDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateAutomationRuleCommandHandler(
        ICurrentUserContext currentUserContext,
        IWorkspaceRepository workspaceRepository,
        ISocialAccountRepository socialAccountRepository,
        IAutomationRuleRepository automationRuleRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _workspaceRepository = workspaceRepository;
        _socialAccountRepository = socialAccountRepository;
        _automationRuleRepository = automationRuleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<AutomationRuleDetailDto>> Handle(
        CreateAutomationRuleCommand request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<AutomationRuleDetailDto>.Fail(access.Error!, access.Code!.Value);
        }

        var workspace = await _workspaceRepository.GetByIdAsync(_currentUserContext.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return ContentGuard.NotFound<AutomationRuleDetailDto>("Workspace");
        }

        if (workspace.Plan is not SubscriptionPlan.Business and not SubscriptionPlan.Enterprise)
        {
            return Result<AutomationRuleDetailDto>.Fail(
                "Automation rules are only supported on Business or Enterprise plans.",
                ErrorCode.Forbidden);
        }

        var socialAccount = await _socialAccountRepository.GetByIdAsync(request.Request.SocialAccountId, cancellationToken);
        if (socialAccount is null || socialAccount.WorkspaceId != _currentUserContext.WorkspaceId)
        {
            return ContentGuard.NotFound<AutomationRuleDetailDto>("Social account");
        }

        var dummyRuleId = Guid.Empty;
        var conditions = request.Request.Conditions.Select(c => TriggerCondition.Create(
            dummyRuleId,
            c.Type,
            c.Operator,
            c.Value,
            c.IsCaseSensitive
        )).ToList();

        var actions = request.Request.Actions.Select(a => AutomationAction.Create(
            dummyRuleId,
            a.Type,
            a.ExecutionOrder,
            a.DelaySeconds,
            a.MessageTemplate,
            a.LinkUrl
        )).ToList();

        var rule = AutomationRule.Create(
            _currentUserContext.WorkspaceId,
            request.Request.SocialAccountId,
            request.Request.Name,
            request.Request.Description,
            request.Request.TriggerType,
            request.Request.TargetExternalPostId,
            request.Request.MaxActionsPerUser,
            request.Request.DailyExecutionLimit,
            conditions,
            actions,
            DateTime.UtcNow
        );

        // Link SocialAccount navigation for Mapping profile
        typeof(AutomationRule).GetProperty(nameof(AutomationRule.SocialAccount))?
            .SetValue(rule, socialAccount);

        await _automationRuleRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<AutomationRuleDetailDto>(rule);
        return Result<AutomationRuleDetailDto>.Ok(dto);
    }
}

/// <summary>
/// Handles updating definition of an automation rule.
/// </summary>
public sealed class UpdateAutomationRuleCommandHandler
    : IRequestHandler<UpdateAutomationRuleCommand, Result<AutomationRuleDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateAutomationRuleCommandHandler(
        ICurrentUserContext currentUserContext,
        ISocialAccountRepository socialAccountRepository,
        IAutomationRuleRepository automationRuleRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _socialAccountRepository = socialAccountRepository;
        _automationRuleRepository = automationRuleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<AutomationRuleDetailDto>> Handle(
        UpdateAutomationRuleCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound<AutomationRuleDetailDto>("Automation rule");
        }

        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<AutomationRuleDetailDto>.Fail(access.Error!, access.Code!.Value);
        }

        var socialAccount = await _socialAccountRepository.GetByIdAsync(request.Request.SocialAccountId, cancellationToken);
        if (socialAccount is null || socialAccount.WorkspaceId != rule.WorkspaceId)
        {
            return ContentGuard.NotFound<AutomationRuleDetailDto>("Social account");
        }

        var conditions = request.Request.Conditions.Select(c => TriggerCondition.Create(
            rule.Id,
            c.Type,
            c.Operator,
            c.Value,
            c.IsCaseSensitive
        )).ToList();

        var actions = request.Request.Actions.Select(a => AutomationAction.Create(
            rule.Id,
            a.Type,
            a.ExecutionOrder,
            a.DelaySeconds,
            a.MessageTemplate,
            a.LinkUrl
        )).ToList();

        rule.UpdateDefinition(
            request.Request.Name,
            request.Request.Description,
            request.Request.SocialAccountId,
            request.Request.TriggerType,
            request.Request.TargetExternalPostId,
            request.Request.MaxActionsPerUser,
            request.Request.DailyExecutionLimit,
            conditions,
            actions,
            DateTime.UtcNow
        );

        typeof(AutomationRule).GetProperty(nameof(AutomationRule.SocialAccount))?
            .SetValue(rule, socialAccount);

        _automationRuleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<AutomationRuleDetailDto>(rule);
        return Result<AutomationRuleDetailDto>.Ok(dto);
    }
}

/// <summary>
/// Handles enabling or disabling an automation rule.
/// </summary>
public sealed class ToggleAutomationRuleCommandHandler
    : IRequestHandler<ToggleAutomationRuleCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleAutomationRuleCommandHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ToggleAutomationRuleCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound("Automation rule");
        }

        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        rule.SetEnabled(request.IsEnabled, DateTime.UtcNow);
        _automationRuleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Handles deleting an automation rule (only disabled ones are allowed).
/// </summary>
public sealed class DeleteAutomationRuleCommandHandler
    : IRequestHandler<DeleteAutomationRuleCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAutomationRuleCommandHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IPendingDMQueueRepository pendingDMQueueRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteAutomationRuleCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound("Automation rule");
        }

        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        if (rule.IsEnabled)
        {
            return Result.Fail("Only disabled rules can be deleted.", ErrorCode.Conflict);
        }

        // Cancel all waiting DM queue entries belonging to this rule
        var waitingDMs = await _pendingDMQueueRepository.GetWaitingByRuleIdAsync(rule.Id, cancellationToken);
        foreach (var dm in waitingDMs)
        {
            dm.MarkCancelled(DateTime.UtcNow);
            _pendingDMQueueRepository.Update(dm);
        }

        _automationRuleRepository.Remove(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Evaluates active rules against incoming platform trigger event.
/// </summary>
public sealed class EvaluateAutomationRulesCommandHandler
    : IRequestHandler<EvaluateAutomationRulesCommand, Result>
{
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IAutomationExecutionLogRepository _logRepository;
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IConversationAssignmentRepository _conversationAssignmentRepository;
    private readonly IPlatformMessagingService _platformMessagingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EvaluateAutomationRulesCommandHandler> _logger;

    public EvaluateAutomationRulesCommandHandler(
        IAutomationRuleRepository automationRuleRepository,
        IAutomationExecutionLogRepository logRepository,
        IPendingDMQueueRepository pendingDMQueueRepository,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IConversationAssignmentRepository conversationAssignmentRepository,
        IPlatformMessagingService platformMessagingService,
        IUnitOfWork unitOfWork,
        ILogger<EvaluateAutomationRulesCommandHandler> logger)
    {
        _automationRuleRepository = automationRuleRepository;
        _logRepository = logRepository;
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _conversationAssignmentRepository = conversationAssignmentRepository;
        _platformMessagingService = platformMessagingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private sealed record ActionResult(bool IsSuccess, AutomationExecutionOutcome Outcome, string? ErrorMessage = null, Guid? PendingDMQueueId = null);

    public async Task<Result> Handle(
        EvaluateAutomationRulesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Enum.TryParse<AutomationTriggerType>(request.TriggerType, out var triggerType))
            {
                return Result.Fail($"Invalid trigger type: {request.TriggerType}", ErrorCode.Validation);
            }

            var rules = await _automationRuleRepository.GetEnabledByAccountIdAsync(request.SocialAccountId, cancellationToken);
            var matchingRules = rules.Where(r => r.TriggerType == triggerType).ToList();

            if (!matchingRules.Any())
            {
                return Result.Ok();
            }

            NormalizedWebhookEvent? @event = null;
            try
            {
                @event = JsonSerializer.Deserialize<NormalizedWebhookEvent>(request.PayloadJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize webhook event payload.");
                return Result.Fail("Failed to deserialize webhook event payload.", ErrorCode.Validation);
            }

            if (@event is null)
            {
                return Result.Fail("NormalizedWebhookEvent was null.", ErrorCode.Validation);
            }

            var semaphore = new SemaphoreSlim(5);
            var tasks = matchingRules.Select(async rule =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await EvaluateRuleAsync(rule, @event, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during evaluation of rule {RuleId}.", rule.Id);
                    var log = AutomationExecutionLog.Create(
                        rule.Id,
                        request.ExternalEventId,
                        @event.ExternalUserId ?? "unknown",
                        @event.ExternalUserName,
                        @event.ContentText,
                        DateTime.UtcNow,
                        AutomationExecutionOutcome.Failed,
                        null,
                        ex.Message,
                        null
                    );
                    await _logRepository.AddAsync(log, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating automation rules.");
            return Result.Fail("Internal error occurred during rule evaluation.", ErrorCode.Unknown);
        }
    }

    private async Task EvaluateRuleAsync(AutomationRule rule, NormalizedWebhookEvent @event, CancellationToken ct)
    {
        var alreadyProcessed = await _logRepository.ExistsByExternalEventIdAsync(rule.Id, @event.ExternalEventId ?? string.Empty, ct);
        if (alreadyProcessed)
        {
            return;
        }

        if (rule.HasReachedDailyLimit())
        {
            await LogSkipAsync(rule.Id, @event, "DailyExecutionLimitReached", ct);
            return;
        }

        var userActionCount = await _logRepository.CountActionedByRuleAndExternalUserAsync(rule.Id, @event.ExternalUserId ?? string.Empty, ct);
        if (userActionCount >= rule.MaxActionsPerUser)
        {
            await LogSkipAsync(rule.Id, @event, "MaxActionsPerUserLimitReached", ct);
            return;
        }

        foreach (var condition in rule.Conditions)
        {
            var matched = await EvaluateConditionAsync(condition, @event, ct);
            if (!matched)
            {
                await LogSkipAsync(rule.Id, @event, $"ConditionDidNotMatch: {condition.Type}", ct);
                return;
            }
        }

        rule.IncrementTodayExecutionCount(DateTime.UtcNow);
        _automationRuleRepository.Update(rule);

        Guid? pendingDmId = null;
        var outcome = AutomationExecutionOutcome.Executed;
        string? errorMessage = null;

        var orderedActions = rule.Actions.OrderBy(a => a.ExecutionOrder).ToList();
        foreach (var action in orderedActions)
        {
            var result = await ExecuteActionAsync(rule, action, @event, ct);
            if (!result.IsSuccess)
            {
                outcome = result.Outcome;
                errorMessage = result.ErrorMessage;
                pendingDmId = result.PendingDMQueueId;
                break;
            }
        }

        var log = AutomationExecutionLog.Create(
            rule.Id,
            @event.ExternalEventId ?? string.Empty,
            @event.ExternalUserId ?? "unknown",
            @event.ExternalUserName,
            @event.ContentText,
            DateTime.UtcNow,
            outcome,
            null,
            errorMessage,
            pendingDmId
        );
        await _logRepository.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<bool> EvaluateConditionAsync(TriggerCondition condition, NormalizedWebhookEvent @event, CancellationToken ct)
    {
        switch (condition.Type)
        {
            case ConditionType.CommentText:
                return EvaluateStringCondition(@event.ContentText, condition.Operator, condition.Value, condition.IsCaseSensitive);

            case ConditionType.CommentAuthorIsFollower:
                return @event.IsFollowingUs == true;

            case ConditionType.AccountIsPublic:
                return EvaluateAccountIsPublic(@event);

            case ConditionType.FirstTimeCommenter:
                var commentedBefore = await _logRepository.CountActionedByRuleAndExternalUserAsync(condition.AutomationRuleId, @event.ExternalUserId ?? string.Empty, ct) > 0;
                return !commentedBefore;

            default:
                return true;
        }
    }

    private bool EvaluateStringCondition(string? eventValue, ConditionOperator op, string? conditionValue, bool isCaseSensitive)
    {
        if (op == ConditionOperator.Any)
        {
            return true;
        }

        if (eventValue == null)
        {
            return op == ConditionOperator.NotContains;
        }

        var val1 = eventValue;
        var val2 = conditionValue ?? string.Empty;

        var comp = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return op switch
        {
            ConditionOperator.Equals => string.Equals(val1, val2, comp),
            ConditionOperator.Contains => val1.Contains(val2, comp),
            ConditionOperator.NotContains => !val1.Contains(val2, comp),
            ConditionOperator.StartsWith => val1.StartsWith(val2, comp),
            ConditionOperator.EndsWith => val1.EndsWith(val2, comp),
            _ => false
        };
    }

    private bool EvaluateAccountIsPublic(NormalizedWebhookEvent @event)
    {
        try
        {
            using var doc = JsonDocument.Parse(@event.RawPayload);
            if (doc.RootElement.TryGetProperty("is_private", out var privateProp) && privateProp.ValueKind == JsonValueKind.True)
            {
                return false;
            }
            if (doc.RootElement.TryGetProperty("user", out var userEl))
            {
                if (userEl.TryGetProperty("is_private", out var userPrivateProp) && userPrivateProp.ValueKind == JsonValueKind.True)
                {
                    return false;
                }
            }
        }
        catch
        {
            // fallback
        }
        return true;
    }

    private async Task<ActionResult> ExecuteActionAsync(AutomationRule rule, AutomationAction action, NormalizedWebhookEvent @event, CancellationToken ct)
    {
        var socialAccount = rule.SocialAccount;

        switch (action.Type)
        {
            case ActionType.SendDirectMessage:
                {
                    var resolvedText = ResolveTemplate(action.MessageTemplate, action.LinkUrl, @event);

                    var targetIsPrivate = !EvaluateAccountIsPublic(@event);
                    var targetIsFollowingUs = @event.IsFollowingUs == true;

                    if (targetIsPrivate && !targetIsFollowingUs)
                    {
                        var expiresAt = DateTime.UtcNow.AddDays(7);
                        var pendingDM = PendingDMQueue.Create(
                            rule.Id,
                            socialAccount.Id,
                            @event.ExternalUserId ?? string.Empty,
                            @event.ExternalUserName,
                            resolvedText,
                            PendingReason.TargetAccountIsPrivate,
                            DateTime.UtcNow,
                            expiresAt
                        );
                        await _pendingDMQueueRepository.AddAsync(pendingDM, ct);
                        await _unitOfWork.SaveChangesAsync(ct);

                        return new ActionResult(false, AutomationExecutionOutcome.Pending, "Direct message deferred because target profile is private and not following.", pendingDM.Id);
                    }
                    else
                    {
                        var sendResult = await _platformMessagingService.SendDirectMessageAsync(socialAccount, @event.ExternalUserId ?? string.Empty, resolvedText, ct);
                        if (!sendResult.IsSuccess)
                        {
                            return new ActionResult(false, AutomationExecutionOutcome.Failed, sendResult.ErrorMessage);
                        }

                        var conversation = await GetOrCreateConversationAsync(rule.WorkspaceId, socialAccount.Id, @event.ExternalUserId ?? string.Empty, @event.ExternalUserName, DateTime.UtcNow, ct);

                        var outboundMsg = InboxMessage.CreateOutbound(
                            conversation.Id,
                            sendResult.ExternalMessageId ?? Guid.NewGuid().ToString(),
                            null,
                            true,
                            rule.Id,
                            MessageContentType.Text,
                            resolvedText,
                            null,
                            sendResult.SentAtUtc ?? DateTime.UtcNow
                        );
                        await _inboxMessageRepository.AddAsync(outboundMsg, ct);

                        conversation.RegisterOutboundMessage(resolvedText, sendResult.SentAtUtc ?? DateTime.UtcNow);
                        _inboxConversationRepository.Update(conversation);

                        return new ActionResult(true, AutomationExecutionOutcome.Executed);
                    }
                }

            case ActionType.ReplyToComment:
                {
                    var resolvedText = ResolveTemplate(action.MessageTemplate, action.LinkUrl, @event);
                    var replyResult = await _platformMessagingService.ReplyToCommentAsync(socialAccount, @event.ExternalConversationId ?? string.Empty, resolvedText, ct);
                    if (!replyResult.IsSuccess)
                    {
                        return new ActionResult(false, AutomationExecutionOutcome.Failed, replyResult.ErrorMessage);
                    }
                    return new ActionResult(true, AutomationExecutionOutcome.Executed);
                }

            case ActionType.LikeComment:
                {
                    var likeResult = await _platformMessagingService.LikeCommentAsync(socialAccount, @event.ExternalConversationId ?? string.Empty, ct);
                    if (!likeResult.IsSuccess)
                    {
                        return new ActionResult(false, AutomationExecutionOutcome.Failed, likeResult.ErrorMessage);
                    }
                    return new ActionResult(true, AutomationExecutionOutcome.Executed);
                }

            case ActionType.AssignToTeamMember:
                {
                    if (Guid.TryParse(action.MessageTemplate, out var assignedToUserId))
                    {
                        var conversation = await GetOrCreateConversationAsync(rule.WorkspaceId, socialAccount.Id, @event.ExternalUserId ?? string.Empty, @event.ExternalUserName, DateTime.UtcNow, ct);

                        var existing = await _conversationAssignmentRepository.GetByConversationIdAsync(conversation.Id, ct);
                        if (existing is not null)
                        {
                            _conversationAssignmentRepository.Remove(existing);
                        }

                        var assignment = ConversationAssignment.Create(
                            conversation.Id,
                            assignedToUserId,
                            null,
                            DateTime.UtcNow,
                            "Assigned by automation rule."
                        );
                        await _conversationAssignmentRepository.AddAsync(assignment, ct);
                    }
                    return new ActionResult(true, AutomationExecutionOutcome.Executed);
                }

            case ActionType.AddConversationTag:
                return new ActionResult(true, AutomationExecutionOutcome.Executed);

            default:
                return new ActionResult(true, AutomationExecutionOutcome.Executed);
        }
    }

    private string ResolveTemplate(string? template, string? linkUrl, NormalizedWebhookEvent @event)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var text = template;
        text = text.Replace("{{username}}", @event.ExternalUserName ?? "user", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{{comment_text}}", @event.ContentText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{{link}}", linkUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("{{post_url}}", $"https://instagram.com/p/{@event.ExternalPostId ?? "post"}", StringComparison.OrdinalIgnoreCase);

        return text;
    }

    private async Task<InboxConversation> GetOrCreateConversationAsync(
        Guid workspaceId,
        Guid socialAccountId,
        string externalUserId,
        string? externalUserName,
        DateTime utcNow,
        CancellationToken ct)
    {
        var conversation = await _inboxConversationRepository.GetByExternalUserIdAsync(socialAccountId, externalUserId, ct);
        if (conversation is null)
        {
            conversation = InboxConversation.Create(
                workspaceId,
                socialAccountId,
                ConversationType.DirectMessage,
                externalUserId,
                externalUserId,
                externalUserName,
                null,
                utcNow
            );
            await _inboxConversationRepository.AddAsync(conversation, ct);
        }
        return conversation;
    }

    private async Task LogSkipAsync(Guid ruleId, NormalizedWebhookEvent @event, string skipReason, CancellationToken ct)
    {
        var log = AutomationExecutionLog.Create(
            ruleId,
            @event.ExternalEventId ?? string.Empty,
            @event.ExternalUserId ?? "unknown",
            @event.ExternalUserName,
            @event.ContentText,
            DateTime.UtcNow,
            AutomationExecutionOutcome.Skipped,
            skipReason,
            null,
            null
        );
        await _logRepository.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Adds a DM to the waiting queue.
/// </summary>
public sealed class EnqueuePendingDMCommandHandler
    : IRequestHandler<EnqueuePendingDMCommand, Result>
{
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EnqueuePendingDMCommandHandler(
        IAutomationRuleRepository automationRuleRepository,
        ISocialAccountRepository socialAccountRepository,
        IPendingDMQueueRepository pendingDMQueueRepository,
        IUnitOfWork unitOfWork)
    {
        _automationRuleRepository = automationRuleRepository;
        _socialAccountRepository = socialAccountRepository;
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        EnqueuePendingDMCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound("Automation rule");
        }

        var socialAccount = await _socialAccountRepository.GetByIdAsync(request.SocialAccountId, cancellationToken);
        if (socialAccount is null || socialAccount.WorkspaceId != rule.WorkspaceId)
        {
            return ContentGuard.NotFound("Social account");
        }

        var expiresAt = request.ExpiresAtUtc ?? DateTime.UtcNow.AddDays(7);
        var entry = PendingDMQueue.Create(
            request.RuleId,
            request.SocialAccountId,
            request.ExternalUserId,
            request.ExternalUserName,
            request.ResolvedMessageText,
            request.Reason,
            DateTime.UtcNow,
            expiresAt
        );

        await _pendingDMQueueRepository.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Recurring job to process waiting pending DMs.
/// </summary>
public sealed class ProcessPendingDMsCommandHandler
    : IRequestHandler<ProcessPendingDMsCommand, Result>
{
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IPlatformMessagingService _platformMessagingService;
    private readonly IInboxConversationRepository _inboxConversationRepository;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPendingDMsCommandHandler> _logger;

    public ProcessPendingDMsCommandHandler(
        IPendingDMQueueRepository pendingDMQueueRepository,
        IPlatformMessagingService platformMessagingService,
        IInboxConversationRepository inboxConversationRepository,
        IInboxMessageRepository inboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPendingDMsCommandHandler> logger)
    {
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _platformMessagingService = platformMessagingService;
        _inboxConversationRepository = inboxConversationRepository;
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        ProcessPendingDMsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Transition expired entries
            var expired = await _pendingDMQueueRepository.GetExpiredAsync(DateTime.UtcNow, cancellationToken);
            foreach (var entry in expired)
            {
                entry.MarkExpired(DateTime.UtcNow);
                _pendingDMQueueRepository.Update(entry);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Fetch waiting ones
            var waiting = await _pendingDMQueueRepository.GetWaitingAsync(cancellationToken);
            if (!waiting.Any())
            {
                return Result.Ok();
            }

            var semaphore = new SemaphoreSlim(5);
            var tasks = waiting.Select(async entry =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Exponential backoff check
                    if (entry.CheckAttemptCount > 0)
                    {
                        var delayMinutes = 30 * Math.Pow(2, Math.Min(entry.CheckAttemptCount, 5));
                        var nextAllowedCheck = entry.LastCheckedAt?.AddMinutes(delayMinutes) ?? DateTime.UtcNow;
                        if (DateTime.UtcNow < nextAllowedCheck)
                        {
                            return;
                        }
                    }

                    entry.MarkChecked(DateTime.UtcNow);

                    // Mock checking follow state (or implement platform specific check if available)
                    var userFollows = false; // default mock

                    if (userFollows)
                    {
                        var sendResult = await _platformMessagingService.SendDirectMessageAsync(
                            entry.SocialAccount,
                            entry.ExternalUserId,
                            entry.ResolvedMessageText,
                            cancellationToken);

                        if (sendResult.IsSuccess)
                        {
                            entry.MarkSent(DateTime.UtcNow);
                            _pendingDMQueueRepository.Update(entry);

                            var conversation = await GetOrCreateConversationAsync(
                                entry.AutomationRule.WorkspaceId,
                                entry.SocialAccountId,
                                entry.ExternalUserId,
                                entry.ExternalUserName,
                                DateTime.UtcNow,
                                cancellationToken);

                            var outboundMsg = InboxMessage.CreateOutbound(
                                conversation.Id,
                                sendResult.ExternalMessageId ?? Guid.NewGuid().ToString(),
                                null,
                                true,
                                entry.AutomationRuleId,
                                MessageContentType.Text,
                                entry.ResolvedMessageText,
                                null,
                                sendResult.SentAtUtc ?? DateTime.UtcNow
                            );
                            await _inboxMessageRepository.AddAsync(outboundMsg, cancellationToken);

                            conversation.RegisterOutboundMessage(entry.ResolvedMessageText, sendResult.SentAtUtc ?? DateTime.UtcNow);
                            _inboxConversationRepository.Update(conversation);
                        }
                        else
                        {
                            _pendingDMQueueRepository.Update(entry);
                        }
                    }
                    else
                    {
                        _pendingDMQueueRepository.Update(entry);
                    }

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process pending DM entry {EntryId}.", entry.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending DM queue.");
            return Result.Fail("Internal error occurred during queue processing.", ErrorCode.Unknown);
        }
    }

    private async Task<InboxConversation> GetOrCreateConversationAsync(
        Guid workspaceId,
        Guid socialAccountId,
        string externalUserId,
        string? externalUserName,
        DateTime utcNow,
        CancellationToken ct)
    {
        var conversation = await _inboxConversationRepository.GetByExternalUserIdAsync(socialAccountId, externalUserId, ct);
        if (conversation is null)
        {
            conversation = InboxConversation.Create(
                workspaceId,
                socialAccountId,
                ConversationType.DirectMessage,
                externalUserId,
                externalUserId,
                externalUserName,
                null,
                utcNow
            );
            await _inboxConversationRepository.AddAsync(conversation, ct);
        }
        return conversation;
    }
}

/// <summary>
/// Cancels a pending DM queue entry.
/// </summary>
public sealed class CancelPendingDMCommandHandler
    : IRequestHandler<CancelPendingDMCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPendingDMCommandHandler(
        ICurrentUserContext currentUserContext,
        IPendingDMQueueRepository pendingDMQueueRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserContext = currentUserContext;
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        CancelPendingDMCommand request,
        CancellationToken cancellationToken)
    {
        var entry = await _pendingDMQueueRepository.GetByIdAsync(request.PendingDmId, cancellationToken);
        if (entry is null)
        {
            return ContentGuard.NotFound("Pending DM queue entry");
        }

        var access = await ContentGuard.RequireContentWriteAccessAsync(
            _currentUserContext.UserId,
            entry.AutomationRule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        if (entry.Status != PendingDMStatus.Waiting)
        {
            return Result.Fail("Only waiting entries can be cancelled.", ErrorCode.Conflict);
        }

        entry.MarkCancelled(DateTime.UtcNow);
        _pendingDMQueueRepository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Writes execution logs for automation rule outcomes.
/// </summary>
public sealed class LogAutomationExecutionCommandHandler
    : IRequestHandler<LogAutomationExecutionCommand, Result>
{
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IAutomationExecutionLogRepository _logRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LogAutomationExecutionCommandHandler(
        IAutomationRuleRepository automationRuleRepository,
        IAutomationExecutionLogRepository logRepository,
        IUnitOfWork unitOfWork)
    {
        _automationRuleRepository = automationRuleRepository;
        _logRepository = logRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        LogAutomationExecutionCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound("Automation rule");
        }

        var log = AutomationExecutionLog.Create(
            request.RuleId,
            request.ExternalTriggerEventId,
            request.TriggerExternalUserId,
            request.TriggerUserName,
            request.TriggerContent,
            DateTime.UtcNow,
            request.Outcome,
            request.SkipReason,
            request.ErrorMessage,
            request.PendingDMQueueId
        );

        await _logRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}

/// <summary>
/// Retrieves a paginated list of rules in the workspace.
/// </summary>
public sealed class GetAutomationRulesQueryHandler
    : IRequestHandler<GetAutomationRulesQuery, Result<PagedResult<AutomationRuleDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    public GetAutomationRulesQueryHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<AutomationRuleDto>>> Handle(
        GetAutomationRulesQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<AutomationRuleDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var rules = await _automationRuleRepository.GetByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<AutomationRuleDto>>(rules);

        var total = dtos.Count;
        var items = dtos
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToList();

        var result = new PagedResult<AutomationRuleDto>(items, total, request.Pagination.Page, request.Pagination.PageSize);
        return Result<PagedResult<AutomationRuleDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves rule detail with conditions and actions.
/// </summary>
public sealed class GetAutomationRuleByIdQueryHandler
    : IRequestHandler<GetAutomationRuleByIdQuery, Result<AutomationRuleDetailDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    public GetAutomationRuleByIdQueryHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    public async Task<Result<AutomationRuleDetailDto>> Handle(
        GetAutomationRuleByIdQuery request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound<AutomationRuleDetailDto>("Automation rule");
        }

        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<AutomationRuleDetailDto>.Fail(access.Error!, access.Code!.Value);
        }

        var dto = _mapper.Map<AutomationRuleDetailDto>(rule);
        return Result<AutomationRuleDetailDto>.Ok(dto);
    }
}

/// <summary>
/// Lists available conditions.
/// </summary>
public sealed class GetAvailableConditionsQueryHandler
    : IRequestHandler<GetAvailableConditionsQuery, Result<IReadOnlyList<AvailableConditionDto>>>
{
    public Task<Result<IReadOnlyList<AvailableConditionDto>>> Handle(
        GetAvailableConditionsQuery request,
        CancellationToken cancellationToken)
    {
        var list = new List<AvailableConditionDto>
        {
            new(ConditionType.CommentText.ToString(), "Comment Text", "Matches keywords or phrases in the comment."),
            new(ConditionType.CommentAuthorIsFollower.ToString(), "Is Follower", "Checks if the commenter is following your page."),
            new(ConditionType.AccountIsPublic.ToString(), "Public Profile", "Checks if the commenter has a public profile."),
            new(ConditionType.FirstTimeCommenter.ToString(), "First Time Commenter", "Checks if this is the first comment from this user.")
        };

        return Task.FromResult(Result<IReadOnlyList<AvailableConditionDto>>.Ok(list));
    }
}

/// <summary>
/// Retrieves a paginated list of pending DMs.
/// </summary>
public sealed class GetPendingDMsQueryHandler
    : IRequestHandler<GetPendingDMsQuery, Result<PagedResult<PendingDMQueueDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPendingDMQueueRepository _pendingDMQueueRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    public GetPendingDMsQueryHandler(
        ICurrentUserContext currentUserContext,
        IPendingDMQueueRepository pendingDMQueueRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _pendingDMQueueRepository = pendingDMQueueRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<PendingDMQueueDto>>> Handle(
        GetPendingDMsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<PendingDMQueueDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var total = await _pendingDMQueueRepository.CountByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            request.Status,
            cancellationToken);

        var items = await _pendingDMQueueRepository.GetPagedByWorkspaceIdAsync(
            _currentUserContext.WorkspaceId,
            request.Status,
            (request.Pagination.Page - 1) * request.Pagination.PageSize,
            request.Pagination.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<PendingDMQueueDto>>(items);

        var result = new PagedResult<PendingDMQueueDto>(dtos, total, request.Pagination.Page, request.Pagination.PageSize);
        return Result<PagedResult<PendingDMQueueDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves paginated execution logs.
/// </summary>
public sealed class GetExecutionLogsQueryHandler
    : IRequestHandler<GetExecutionLogsQuery, Result<PagedResult<ExecutionLogDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IAutomationExecutionLogRepository _logRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    public GetExecutionLogsQueryHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IAutomationExecutionLogRepository logRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _logRepository = logRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    public async Task<Result<PagedResult<ExecutionLogDto>>> Handle(
        GetExecutionLogsQuery request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound<PagedResult<ExecutionLogDto>>("Automation rule");
        }

        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<ExecutionLogDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var total = await _logRepository.CountFilteredByRuleIdAsync(
            request.RuleId,
            request.Outcome,
            request.From,
            request.To,
            cancellationToken);

        var items = await _logRepository.GetFilteredByRuleIdAsync(
            request.RuleId,
            request.Outcome,
            request.From,
            request.To,
            (request.Pagination.Page - 1) * request.Pagination.PageSize,
            request.Pagination.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<ExecutionLogDto>>(items);

        var result = new PagedResult<ExecutionLogDto>(dtos, total, request.Pagination.Page, request.Pagination.PageSize);
        return Result<PagedResult<ExecutionLogDto>>.Ok(result);
    }
}

/// <summary>
/// Retrieves rule effectiveness statistics.
/// </summary>
public sealed class GetRuleEffectivenessQueryHandler
    : IRequestHandler<GetRuleEffectivenessQuery, Result<AutomationRuleEffectivenessDto>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAutomationRuleRepository _automationRuleRepository;
    private readonly IAutomationExecutionLogRepository _logRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;

    public GetRuleEffectivenessQueryHandler(
        ICurrentUserContext currentUserContext,
        IAutomationRuleRepository automationRuleRepository,
        IAutomationExecutionLogRepository logRepository,
        IWorkspaceMemberRepository workspaceMemberRepository)
    {
        _currentUserContext = currentUserContext;
        _automationRuleRepository = automationRuleRepository;
        _logRepository = logRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
    }

    public async Task<Result<AutomationRuleEffectivenessDto>> Handle(
        GetRuleEffectivenessQuery request,
        CancellationToken cancellationToken)
    {
        var rule = await _automationRuleRepository.GetByIdWithDetailsAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return ContentGuard.NotFound<AutomationRuleEffectivenessDto>("Automation rule");
        }

        var access = await ContentGuard.RequireReadAccessAsync(
            _currentUserContext.UserId,
            rule.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<AutomationRuleEffectivenessDto>.Fail(access.Error!, access.Code!.Value);
        }

        var successful = await _logRepository.CountByRuleAndOutcomeAsync(rule.Id, AutomationExecutionOutcome.Executed, cancellationToken);
        var skipped = await _logRepository.CountByRuleAndOutcomeAsync(rule.Id, AutomationExecutionOutcome.Skipped, cancellationToken);
        var failed = await _logRepository.CountByRuleAndOutcomeAsync(rule.Id, AutomationExecutionOutcome.Failed, cancellationToken);
        var pending = await _logRepository.CountByRuleAndOutcomeAsync(rule.Id, AutomationExecutionOutcome.Pending, cancellationToken);

        var total = successful + skipped + failed + pending;
        var dto = new AutomationRuleEffectivenessDto(rule.Id, total, successful, skipped + pending, failed);

        return Result<AutomationRuleEffectivenessDto>.Ok(dto);
    }
}
