using Application.Abstractions.Webhooks;
using Domain.Enums;

namespace Infrastructure.Webhooks;

/// <summary>
/// Возвращает зарегистрированный parser для конкретной webhook-платформы.
/// </summary>
public sealed class WebhookPayloadParserFactory : IWebhookPayloadParserFactory
{
    private readonly IReadOnlyDictionary<Platform, IWebhookPayloadParser> _parsers;

    /// <summary>
    /// Инициализирует фабрику списком зарегистрированных parser-реализаций.
    /// </summary>
    public WebhookPayloadParserFactory(IEnumerable<IWebhookPayloadParser> parsers)
    {
        _parsers = parsers.ToDictionary(parser => parser.Platform);
    }

    /// <inheritdoc />
    public IWebhookPayloadParser GetParser(Platform platform)
    {
        if (_parsers.TryGetValue(platform, out var parser))
        {
            return parser;
        }

        throw new NotSupportedException($"Webhook payload parser is not registered for platform '{platform}'.");
    }
}
