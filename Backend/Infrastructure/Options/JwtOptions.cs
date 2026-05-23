using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Defines configuration values used to generate and validate JWT access tokens.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the HMAC signing secret used for access token creation and validation.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical issuer written into generated JWT tokens.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical audience written into generated JWT tokens.
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token lifetime in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
