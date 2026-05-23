using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Contains outbound OAuth/API configuration for every supported social platform.
/// </summary>
public sealed class PlatformOAuthOptions : IValidatableObject
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "PlatformOAuth";

    /// <summary>
    /// Gets or sets the YouTube provider configuration.
    /// </summary>
    public OAuthProviderOptions YouTube { get; set; } = new();

    /// <summary>
    /// Gets or sets the Instagram provider configuration.
    /// </summary>
    public OAuthProviderOptions Instagram { get; set; } = new();

    /// <summary>
    /// Gets or sets the Facebook provider configuration.
    /// </summary>
    public OAuthProviderOptions Facebook { get; set; } = new();

    /// <summary>
    /// Gets or sets the TikTok provider configuration.
    /// </summary>
    public OAuthProviderOptions TikTok { get; set; } = new();

    /// <summary>
    /// Gets or sets the Twitter/X provider configuration.
    /// </summary>
    public OAuthProviderOptions Twitter { get; set; } = new();

    /// <summary>
    /// Gets or sets the Telegram provider configuration.
    /// </summary>
    public OAuthProviderOptions Telegram { get; set; } = new();

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, provider) in GetAllProviders())
        {
            if (!provider.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(provider.ClientId))
            {
                yield return new ValidationResult(
                    $"{name}:ClientId is required when the provider is enabled.",
                    [$"{name}.ClientId"]);
            }

            if (string.IsNullOrWhiteSpace(provider.ClientSecret))
            {
                yield return new ValidationResult(
                    $"{name}:ClientSecret is required when the provider is enabled.",
                    [$"{name}.ClientSecret"]);
            }

            if (string.IsNullOrWhiteSpace(provider.AuthorizationEndpoint))
            {
                yield return new ValidationResult(
                    $"{name}:AuthorizationEndpoint is required when the provider is enabled.",
                    [$"{name}.AuthorizationEndpoint"]);
            }

            if (string.IsNullOrWhiteSpace(provider.TokenEndpoint))
            {
                yield return new ValidationResult(
                    $"{name}:TokenEndpoint is required when the provider is enabled.",
                    [$"{name}.TokenEndpoint"]);
            }

            if (string.IsNullOrWhiteSpace(provider.ApiBaseUrl))
            {
                yield return new ValidationResult(
                    $"{name}:ApiBaseUrl is required when the provider is enabled.",
                    [$"{name}.ApiBaseUrl"]);
            }
        }
    }

    /// <summary>
    /// Returns all configured providers paired with their logical names.
    /// </summary>
    /// <returns>The provider collection used by validation and registration code.</returns>
    public IEnumerable<KeyValuePair<string, OAuthProviderOptions>> GetAllProviders()
    {
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(YouTube), YouTube);
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(Instagram), Instagram);
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(Facebook), Facebook);
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(TikTok), TikTok);
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(Twitter), Twitter);
        yield return new KeyValuePair<string, OAuthProviderOptions>(nameof(Telegram), Telegram);
    }
}

/// <summary>
/// Describes OAuth/API settings for a single social platform.
/// </summary>
public sealed class OAuthProviderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether this provider is enabled for runtime use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the client identifier issued by the external provider.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret issued by the external provider.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authorization endpoint used to start OAuth flows.
    /// </summary>
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token endpoint used to exchange authorization codes for tokens.
    /// </summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL used for platform API calls.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the callback path expected by this provider.
    /// </summary>
    public string CallbackPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of scopes requested during authorization.
    /// </summary>
    public string[] Scopes { get; set; } = [];
}
