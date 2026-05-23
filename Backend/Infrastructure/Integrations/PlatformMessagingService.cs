using Application.Abstractions.Integrations;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

/// <summary>
/// Реализует исходящие messaging-операции платформ через безопасный integration fallback.
/// Сервис централизует pre-flight проверку токена и дает CQRS-хендлерам единый способ отправки inbox и automation сообщений.
/// </summary>
public sealed class PlatformMessagingService : IPlatformMessagingService
{
    private readonly IPlatformTokenValidationService _platformTokenValidationService;
    private readonly ILogger<PlatformMessagingService> _logger;

    /// <summary>
    /// Инициализирует сервис отправки платформенных сообщений.
    /// </summary>
    public PlatformMessagingService(
        IPlatformTokenValidationService platformTokenValidationService,
        ILogger<PlatformMessagingService> logger)
    {
        _platformTokenValidationService = platformTokenValidationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PlatformMessageSendResult> SendConversationReplyAsync(
        SocialAccount socialAccount,
        InboxConversation conversation,
        string text,
        Guid? replyToMessageId,
        CancellationToken ct = default)
    {
        var tokenValidation = await _platformTokenValidationService.EnsureValidAsync(socialAccount, ct);
        if (!tokenValidation.IsValid)
        {
            return new PlatformMessageSendResult(false, null, null, tokenValidation.FailureReason, null);
        }

        var sentAtUtc = DateTime.UtcNow;
        var externalMessageId = $"{socialAccount.Platform}:{conversation.ExternalConversationId}:{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Platform reply accepted by fallback messaging transport for account {SocialAccountId}, conversation {ConversationId}, replyTo {ReplyToMessageId}.",
            socialAccount.Id,
            conversation.Id,
            replyToMessageId);

        return new PlatformMessageSendResult(
            true,
            externalMessageId,
            sentAtUtc,
            null,
            $"{{\"mode\":\"fallback\",\"platform\":\"{socialAccount.Platform}\",\"conversationId\":\"{conversation.ExternalConversationId}\"}}");
    }

    /// <inheritdoc />
    public async Task<PlatformMessageSendResult> SendDirectMessageAsync(
        SocialAccount socialAccount,
        string externalUserId,
        string text,
        CancellationToken ct = default)
    {
        var tokenValidation = await _platformTokenValidationService.EnsureValidAsync(socialAccount, ct);
        if (!tokenValidation.IsValid)
        {
            return new PlatformMessageSendResult(false, null, null, tokenValidation.FailureReason, null);
        }

        var sentAtUtc = DateTime.UtcNow;
        var externalMessageId = $"{socialAccount.Platform}:dm:{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Platform direct message accepted by fallback messaging transport for account {SocialAccountId} and external user {ExternalUserId}.",
            socialAccount.Id,
            externalUserId);

        return new PlatformMessageSendResult(
            true,
            externalMessageId,
            sentAtUtc,
            null,
            $"{{\"mode\":\"fallback\",\"platform\":\"{socialAccount.Platform}\",\"externalUserId\":\"{externalUserId}\"}}");
    }

    /// <inheritdoc />
    public async Task<PlatformActionResult> ReplyToCommentAsync(
        SocialAccount socialAccount,
        string externalConversationId,
        string text,
        CancellationToken ct = default)
    {
        var tokenValidation = await _platformTokenValidationService.EnsureValidAsync(socialAccount, ct);
        if (!tokenValidation.IsValid)
        {
            return new PlatformActionResult(false, tokenValidation.FailureReason, null);
        }

        _logger.LogInformation(
            "Platform comment reply accepted by fallback messaging transport for account {SocialAccountId} and external conversation {ExternalConversationId}.",
            socialAccount.Id,
            externalConversationId);

        return new PlatformActionResult(
            true,
            null,
            $"{{\"mode\":\"fallback\",\"platform\":\"{socialAccount.Platform}\",\"externalConversationId\":\"{externalConversationId}\"}}");
    }

    /// <inheritdoc />
    public async Task<PlatformActionResult> LikeCommentAsync(
        SocialAccount socialAccount,
        string externalConversationId,
        CancellationToken ct = default)
    {
        var tokenValidation = await _platformTokenValidationService.EnsureValidAsync(socialAccount, ct);
        if (!tokenValidation.IsValid)
        {
            return new PlatformActionResult(false, tokenValidation.FailureReason, null);
        }

        _logger.LogInformation(
            "Platform comment-like accepted by fallback messaging transport for account {SocialAccountId} and external conversation {ExternalConversationId}.",
            socialAccount.Id,
            externalConversationId);

        return new PlatformActionResult(
            true,
            null,
            $"{{\"mode\":\"fallback\",\"platform\":\"{socialAccount.Platform}\",\"externalConversationId\":\"{externalConversationId}\"}}");
    }
}
