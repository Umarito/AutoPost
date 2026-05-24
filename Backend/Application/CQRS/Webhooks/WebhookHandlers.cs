using Application.Abstractions.Persistence;
using Application.Abstractions.Repositories;
using Application.Abstractions.Webhooks;
using Application.Common;
using Application.Common.Guards;
using Application.DTOs.Webhook;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Webhooks;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  WEBHOOK COMMAND HANDLERS                                                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Persists a received webhook event for later asynchronous processing.
/// This handler is designed for speed — it saves the raw JSON payload immediately
/// and returns the event ID so the controller can respond 200 OK within the 200ms limit.
/// Signature verification is performed inline; parsing is deferred to background processing.
/// </summary>
public sealed class ReceiveWebhookCommandHandler
    : IRequestHandler<ReceiveWebhookCommand, Result<Guid>>
{
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReceiveWebhookCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiveWebhookCommandHandler"/> class.
    /// </summary>
    public ReceiveWebhookCommandHandler(
        IWebhookEventRepository webhookEventRepository,
        IWebhookSignatureVerifier signatureVerifier,
        IUnitOfWork unitOfWork,
        ILogger<ReceiveWebhookCommandHandler> logger)
    {
        _webhookEventRepository = webhookEventRepository;
        _signatureVerifier = signatureVerifier;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> Handle(
        ReceiveWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // Parse the platform enum from the string.
        if (!Enum.TryParse<Platform>(request.Platform, ignoreCase: true, out var platform))
        {
            _logger.LogWarning("Received webhook from unsupported platform: {Platform}", request.Platform);
            return Result<Guid>.Fail(
                $"Unsupported platform: {request.Platform}",
                ErrorCode.Validation);
        }

        // Verify the HMAC-SHA256 signature inline (fast operation).
        var isVerified = _signatureVerifier.Verify(platform, request.RawPayload, request.Signature);

        if (!isVerified)
        {
            _logger.LogWarning(
                "Webhook signature verification failed for platform {Platform}, event type {EventType}.",
                platform, request.EventType);
        }

        // Create and persist the event immediately — speed is critical.
        var webhookEvent = WebhookEvent.Create(
            platform,
            request.EventType,
            request.RawPayload,
            request.Signature,
            isVerified,
            DateTime.UtcNow);

        await _webhookEventRepository.AddAsync(webhookEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Webhook event {WebhookEventId} persisted for platform {Platform}, type {EventType}, verified: {IsVerified}.",
            webhookEvent.Id, platform, request.EventType, isVerified);

        return Result<Guid>.Ok(webhookEvent.Id);
    }
}

/// <summary>
/// Processes a previously stored webhook event by parsing the raw payload,
/// resolving the affected social account, and dispatching downstream actions
/// (creating inbox conversations, evaluating automation rules, etc.).
/// Typically invoked by a background Hangfire job.
/// </summary>
public sealed class ProcessWebhookCommandHandler
    : IRequestHandler<ProcessWebhookCommand, Result>
{
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly ISocialAccountRepository _socialAccountRepository;
    private readonly IWebhookPayloadParserFactory _parserFactory;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessWebhookCommandHandler"/> class.
    /// </summary>
    public ProcessWebhookCommandHandler(
        IWebhookEventRepository webhookEventRepository,
        ISocialAccountRepository socialAccountRepository,
        IWebhookPayloadParserFactory parserFactory,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<ProcessWebhookCommandHandler> logger)
    {
        _webhookEventRepository = webhookEventRepository;
        _socialAccountRepository = socialAccountRepository;
        _parserFactory = parserFactory;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        ProcessWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var webhookEvent = await _webhookEventRepository.GetByIdAsync(
            request.WebhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            return ContentGuard.NotFound("Webhook event");
        }

        // Only process verified, received events.
        if (!webhookEvent.IsVerified)
        {
            webhookEvent.MarkIgnored(DateTime.UtcNow, "Signature verification failed.");
            _webhookEventRepository.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Fail("Webhook event signature is not verified.", ErrorCode.Forbidden);
        }

        if (webhookEvent.Status is not WebhookEventStatus.Received)
        {
            return Result.Fail(
                $"Webhook event is in '{webhookEvent.Status}' state and cannot be processed.",
                ErrorCode.Conflict);
        }

        webhookEvent.MarkProcessing();
        _webhookEventRepository.Update(webhookEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            // Parse the raw payload using the platform-specific parser.
            var parser = _parserFactory.GetParser(webhookEvent.Platform);
            var parseResult = await parser.ParseAsync(
                webhookEvent.EventType, webhookEvent.RawPayload, cancellationToken);

            if (!parseResult.IsSuccess || parseResult.Event is null)
            {
                webhookEvent.MarkFailed(DateTime.UtcNow,
                    parseResult.Error ?? "Payload parsing returned null.");
                _webhookEventRepository.Update(webhookEvent);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Failed to parse webhook event {WebhookEventId}: {Error}",
                    webhookEvent.Id, parseResult.Error);

                return Result.Fail(
                    parseResult.Error ?? "Failed to parse webhook payload.",
                    ErrorCode.Validation);
            }

            var normalizedEvent = parseResult.Event;

            // Resolve the social account if available.
            if (normalizedEvent.SocialAccountId.HasValue)
            {
                var socialAccount = await _socialAccountRepository.GetByIdAsync(
                    normalizedEvent.SocialAccountId.Value, cancellationToken);

                if (socialAccount is null)
                {
                    webhookEvent.MarkIgnored(DateTime.UtcNow,
                        $"Social account {normalizedEvent.SocialAccountId} not found.");
                    _webhookEventRepository.Update(webhookEvent);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    return Result.Ok(); // Not an error — the account may have been disconnected.
                }

                // Dispatch to inbox: create or update conversation and add inbound message.
                await DispatchToInboxAsync(normalizedEvent, socialAccount, cancellationToken);

                // Dispatch to automation: evaluate rules against the event.
                await DispatchToAutomationAsync(normalizedEvent, cancellationToken);
            }

            webhookEvent.MarkProcessed(DateTime.UtcNow);
            _webhookEventRepository.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully processed webhook event {WebhookEventId}.", webhookEvent.Id);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing webhook event {WebhookEventId}.", webhookEvent.Id);

            webhookEvent.MarkFailed(DateTime.UtcNow, ex.Message);
            _webhookEventRepository.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Fail(
                "Internal error during webhook event processing.",
                ErrorCode.Unknown);
        }
    }

    /// <summary>
    /// Creates or updates an inbox conversation and adds an inbound message from the webhook event.
    /// </summary>
    private async Task DispatchToInboxAsync(
        NormalizedWebhookEvent normalizedEvent,
        SocialAccount socialAccount,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(normalizedEvent.ExternalUserId))
        {
            return; // Cannot create a conversation without an external user.
        }

        var conversationType = string.IsNullOrWhiteSpace(normalizedEvent.ExternalConversationId)
            ? "DirectMessage"
            : normalizedEvent.EventType;

        await _mediator.Send(new Application.CQRS.Inbox.CreateOrUpdateConversationCommand(
            socialAccount.Id,
            normalizedEvent.ExternalConversationId ?? normalizedEvent.ExternalUserId,
            normalizedEvent.Platform.ToString(),
            normalizedEvent.ExternalUserId,
            normalizedEvent.ExternalUserName ?? "Unknown",
            conversationType), ct);
    }

    /// <summary>
    /// Dispatches the normalized event to the automation rules engine.
    /// </summary>
    private async Task DispatchToAutomationAsync(
        NormalizedWebhookEvent normalizedEvent,
        CancellationToken ct)
    {
        if (!normalizedEvent.SocialAccountId.HasValue)
        {
            return;
        }

        await _mediator.Send(new Application.CQRS.Automation.EvaluateAutomationRulesCommand(
            normalizedEvent.SocialAccountId.Value,
            normalizedEvent.EventType,
            normalizedEvent.ExternalEventId ?? string.Empty,
            normalizedEvent.RawPayload), ct);
    }
}

/// <summary>
/// Re-runs processing for a webhook event that previously failed or needs replay.
/// Resets the event status back to Received and dispatches it for reprocessing via Mediator.
/// </summary>
public sealed class ReprocessWebhookEventCommandHandler
    : IRequestHandler<ReprocessWebhookEventCommand, Result>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReprocessWebhookEventCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReprocessWebhookEventCommandHandler"/> class.
    /// </summary>
    public ReprocessWebhookEventCommandHandler(
        ICurrentUserContext currentUserContext,
        IWebhookEventRepository webhookEventRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<ReprocessWebhookEventCommandHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _webhookEventRepository = webhookEventRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        ReprocessWebhookEventCommand request,
        CancellationToken cancellationToken)
    {
        // Only admins/owners can replay webhook events.
        var access = await ContentGuard.RequireManagementAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result.Fail(access.Error!, access.Code!.Value);
        }

        var webhookEvent = await _webhookEventRepository.GetByIdAsync(
            request.WebhookEventId, cancellationToken);

        if (webhookEvent is null)
        {
            return ContentGuard.NotFound("Webhook event");
        }

        // Only failed or ignored events can be replayed.
        if (webhookEvent.Status is not WebhookEventStatus.Failed
            and not WebhookEventStatus.Ignored
            and not WebhookEventStatus.Processed)
        {
            return Result.Fail(
                $"Only Failed, Ignored, or Processed events can be reprocessed. Current status: {webhookEvent.Status}.",
                ErrorCode.Conflict);
        }

        webhookEvent.ResetForReprocess();
        _webhookEventRepository.Update(webhookEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Webhook event {WebhookEventId} reset for reprocessing by user {UserId}.",
            webhookEvent.Id, _currentUserContext.UserId);

        // Dispatch reprocessing via Mediator.
        var processResult = await _mediator.Send(
            new ProcessWebhookCommand(webhookEvent.Id), cancellationToken);

        return processResult;
    }
}

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  WEBHOOK QUERY HANDLERS                                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Validates a provider's webhook subscription challenge request.
/// Platforms like Instagram and Facebook send a GET request with a challenge token
/// during webhook subscription setup. This handler echoes the challenge back
/// to confirm the subscription.
/// </summary>
public sealed class VerifyWebhookSubscriptionQueryHandler
    : IRequestHandler<VerifyWebhookSubscriptionQuery, Result<string>>
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly ILogger<VerifyWebhookSubscriptionQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifyWebhookSubscriptionQueryHandler"/> class.
    /// </summary>
    public VerifyWebhookSubscriptionQueryHandler(
        IWebhookSignatureVerifier signatureVerifier,
        ILogger<VerifyWebhookSubscriptionQueryHandler> logger)
    {
        _signatureVerifier = signatureVerifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<string>> Handle(
        VerifyWebhookSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Platform>(request.Platform, ignoreCase: true, out var platform))
        {
            _logger.LogWarning(
                "Webhook subscription verification for unsupported platform: {Platform}",
                request.Platform);

            return Task.FromResult(Result<string>.Fail(
                $"Unsupported platform: {request.Platform}",
                ErrorCode.Validation));
        }

        // For Meta-based platforms (Instagram, Facebook), the verification flow is:
        // 1. Platform sends GET with hub.mode=subscribe, hub.challenge=<token>, hub.verify_token=<our_token>
        // 2. We verify that hub.verify_token matches our configured token.
        // 3. We echo back hub.challenge to confirm the subscription.
        //
        // The verify_token check is handled by verifying the token against our known secret.
        // Since we use the signature verifier for consistency, we compute a signature
        // from the challenge and compare it — if the token is provided by the platform,
        // it should match the one we registered during setup.
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            // Verify the token using the platform's expected verification method.
            var isValid = _signatureVerifier.Verify(platform, request.Challenge, request.Token);
            if (!isValid)
            {
                _logger.LogWarning(
                    "Webhook subscription verification token mismatch for platform {Platform}.",
                    platform);

                return Task.FromResult(Result<string>.Fail(
                    "Verification token does not match.",
                    ErrorCode.Forbidden));
            }
        }

        _logger.LogInformation(
            "Webhook subscription verified for platform {Platform}.", platform);

        // Echo the challenge back to confirm the subscription.
        return Task.FromResult(Result<string>.Ok(request.Challenge));
    }
}

/// <summary>
/// Retrieves paginated webhook events for operational monitoring.
/// Restricted to workspace Owners and Admins.
/// </summary>
public sealed class GetWebhookEventsQueryHandler
    : IRequestHandler<GetWebhookEventsQuery, Result<PagedResult<WebhookEventDto>>>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebhookEventRepository _webhookEventRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetWebhookEventsQueryHandler"/> class.
    /// </summary>
    public GetWebhookEventsQueryHandler(
        ICurrentUserContext currentUserContext,
        IWebhookEventRepository webhookEventRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IMapper mapper)
    {
        _currentUserContext = currentUserContext;
        _webhookEventRepository = webhookEventRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<WebhookEventDto>>> Handle(
        GetWebhookEventsQuery request,
        CancellationToken cancellationToken)
    {
        // Webhook monitoring is admin-only (contains PII in raw payloads).
        var access = await ContentGuard.RequireManagementAccessAsync(
            _currentUserContext.UserId,
            _currentUserContext.WorkspaceId,
            _workspaceMemberRepository,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<PagedResult<WebhookEventDto>>.Fail(access.Error!, access.Code!.Value);
        }

        var skip = (request.Pagination.Page - 1) * request.Pagination.PageSize;

        var items = await _webhookEventRepository.GetFilteredAsync(
            request.Status,
            request.Platform,
            request.From,
            skip,
            request.Pagination.PageSize,
            cancellationToken);

        var totalCount = await _webhookEventRepository.CountFilteredAsync(
            request.Status,
            request.Platform,
            request.From,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<WebhookEventDto>>(items);
        var result = new PagedResult<WebhookEventDto>(
            dtos, totalCount, request.Pagination.Page, request.Pagination.PageSize);

        return Result<PagedResult<WebhookEventDto>>.Ok(result);
    }
}
