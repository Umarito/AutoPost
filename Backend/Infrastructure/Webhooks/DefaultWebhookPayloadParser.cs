using System.Text.Json;
using Application.Abstractions.Webhooks;
using Domain.Enums;

namespace Infrastructure.Webhooks;

/// <summary>
/// Выполняет tolerant parsing webhook payload в нормализованную модель, пригодную для Application-слоя.
/// </summary>
public sealed class DefaultWebhookPayloadParser : IWebhookPayloadParser
{
    /// <summary>
    /// Инициализирует parser для указанной платформы.
    /// </summary>
    public DefaultWebhookPayloadParser(Platform platform)
    {
        Platform = platform;
    }

    /// <inheritdoc />
    public Platform Platform { get; }

    /// <inheritdoc />
    public Task<WebhookPayloadParseResult> ParseAsync(string eventType, string rawPayload, CancellationToken ct = default)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var root = document.RootElement;

            var normalized = new NormalizedWebhookEvent(
                Platform,
                eventType,
                TryReadString(root, "externalEventId") ??
                TryReadString(root, "eventId") ??
                TryReadString(root, "id"),
                TryReadGuid(root, "socialAccountId"),
                TryReadString(root, "externalConversationId") ??
                TryReadString(root, "conversationId") ??
                TryReadString(root, "threadId"),
                TryReadString(root, "externalMessageId") ??
                TryReadString(root, "messageId"),
                TryReadString(root, "externalUserId") ??
                TryReadString(root, "userId") ??
                TryReadString(root, "fromUserId"),
                TryReadString(root, "externalUserName") ??
                TryReadString(root, "username") ??
                TryReadString(root, "userName"),
                TryReadString(root, "externalUserAvatarUrl") ??
                TryReadString(root, "avatarUrl"),
                TryReadString(root, "externalPostId") ??
                TryReadString(root, "postId"),
                TryReadString(root, "contentText") ??
                TryReadString(root, "text") ??
                TryReadString(root, "message"),
                TryReadBool(root, "isFollowingUs"),
                TryReadDateTime(root, "occurredAtUtc")
                    ?? TryReadDateTime(root, "occurredAt")
                    ?? TryReadDateTime(root, "createdAtUtc")
                    ?? DateTime.UtcNow,
                rawPayload);

            return Task.FromResult(new WebhookPayloadParseResult(true, null, normalized));
        }
        catch (JsonException)
        {
            return Task.FromResult(new WebhookPayloadParseResult(false, "The webhook payload is not valid JSON.", null));
        }
    }

    private static string? TryReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };

    private static Guid? TryReadGuid(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String &&
           Guid.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;

    private static bool? TryReadBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static DateTime? TryReadDateTime(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String &&
           DateTime.TryParse(property.GetString(), out var parsed)
            ? parsed.ToUniversalTime()
            : null;
}
