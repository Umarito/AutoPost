using Application.Abstractions.Webhooks;
using Domain.Enums;
using Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Webhooks;

/// <summary>
/// Validates provider-signed webhook payloads using HMAC-SHA256 secrets from configuration.
/// </summary>
public sealed class WebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly WebhookOptions _webhookOptions;

    /// <summary>
    /// Initializes the verifier with the configured webhook secrets.
    /// </summary>
    /// <param name="webhookOptions">The webhook options bound from configuration.</param>
    public WebhookSignatureVerifier(IOptions<WebhookOptions> webhookOptions)
    {
        _webhookOptions = webhookOptions.Value;
    }

    /// <inheritdoc />
    public bool Verify(Platform platform, string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var provider = GetProvider(platform);
        var expected = ComputeSignature(platform, payload);
        var normalizedReceived = signature.Trim();
        var normalizedExpected = provider.SignaturePrefix + expected;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(normalizedExpected),
            Encoding.UTF8.GetBytes(normalizedReceived));
    }

    /// <inheritdoc />
    public string ComputeSignature(Platform platform, string payload)
    {
        var provider = GetProvider(platform);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(provider.SigningSecret));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    private WebhookProviderOptions GetProvider(Platform platform) => platform switch
    {
        Platform.YouTube => _webhookOptions.YouTube,
        Platform.Instagram => _webhookOptions.Instagram,
        Platform.Facebook => _webhookOptions.Facebook,
        Platform.TikTok => _webhookOptions.TikTok,
        Platform.Twitter => _webhookOptions.Twitter,
        Platform.Telegram => _webhookOptions.Telegram,
        _ => throw new NotSupportedException($"Webhook settings are not defined for platform '{platform}'.")
    };
}
