using Domain.Enums;

namespace Application.Abstractions.Webhooks;

/// <summary>
/// Verifies inbound webhook signatures from social platforms.
///
/// <para>
/// The webhook pipeline must reject spoofed payloads before any downstream
/// processing is scheduled. This contract encapsulates HMAC verification logic
/// and the provider-specific secrets required to validate incoming events.
/// </para>
/// </summary>
public interface IWebhookSignatureVerifier
{
    /// <summary>
    /// Verifies the inbound signature for a raw webhook payload.
    /// </summary>
    /// <param name="platform">The platform that sent the webhook request.</param>
    /// <param name="payload">The raw request payload used as the HMAC body.</param>
    /// <param name="signature">The signature header value received from the platform.</param>
    /// <returns><c>true</c> when the signature is valid; otherwise <c>false</c>.</returns>
    bool Verify(Platform platform, string payload, string? signature);

    /// <summary>
    /// Computes the expected signature for a raw payload and platform secret.
    /// </summary>
    /// <param name="platform">The platform whose secret should be used.</param>
    /// <param name="payload">The raw request payload used as the HMAC body.</param>
    /// <returns>The computed signature string.</returns>
    string ComputeSignature(Platform platform, string payload);
}
