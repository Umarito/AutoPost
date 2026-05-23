using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Contains inbound webhook verification configuration for every supported platform.
/// </summary>
public sealed class WebhookOptions : IValidatableObject
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "Webhooks";

    /// <summary>
    /// Gets or sets the YouTube webhook verification settings.
    /// </summary>
    public WebhookProviderOptions YouTube { get; set; } = new();

    /// <summary>
    /// Gets or sets the Instagram webhook verification settings.
    /// </summary>
    public WebhookProviderOptions Instagram { get; set; } = new();

    /// <summary>
    /// Gets or sets the Facebook webhook verification settings.
    /// </summary>
    public WebhookProviderOptions Facebook { get; set; } = new();

    /// <summary>
    /// Gets or sets the TikTok webhook verification settings.
    /// </summary>
    public WebhookProviderOptions TikTok { get; set; } = new();

    /// <summary>
    /// Gets or sets the Twitter/X webhook verification settings.
    /// </summary>
    public WebhookProviderOptions Twitter { get; set; } = new();

    /// <summary>
    /// Gets or sets the Telegram webhook verification settings.
    /// </summary>
    public WebhookProviderOptions Telegram { get; set; } = new();

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, provider) in GetAllProviders())
        {
            if (provider.Enabled && string.IsNullOrWhiteSpace(provider.SigningSecret))
            {
                yield return new ValidationResult(
                    $"{name}:SigningSecret is required when webhook verification is enabled.",
                    [$"{name}.SigningSecret"]);
            }
        }
    }

    /// <summary>
    /// Returns all webhook providers paired with their logical names.
    /// </summary>
    /// <returns>The provider collection used by validation and verification code.</returns>
    public IEnumerable<KeyValuePair<string, WebhookProviderOptions>> GetAllProviders()
    {
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(YouTube), YouTube);
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(Instagram), Instagram);
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(Facebook), Facebook);
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(TikTok), TikTok);
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(Twitter), Twitter);
        yield return new KeyValuePair<string, WebhookProviderOptions>(nameof(Telegram), Telegram);
    }
}

/// <summary>
/// Describes webhook signature settings for a single provider.
/// </summary>
public sealed class WebhookProviderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether signature verification is enabled for this provider.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the signing secret used to verify inbound webhook payloads.
    /// </summary>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP header that carries the provider signature.
    /// </summary>
    public string SignatureHeaderName { get; set; } = "X-Hub-Signature-256";

    /// <summary>
    /// Gets or sets the expected prefix used by the provider before the raw hex digest.
    /// </summary>
    public string SignaturePrefix { get; set; } = "sha256=";
}
