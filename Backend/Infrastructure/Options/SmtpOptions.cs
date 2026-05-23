using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// SMTP transport settings used for transactional email delivery.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// Gets or sets the SMTP host name.
    /// </summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP port.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = "noreply@autopost.local";

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    [Required]
    public string FromName { get; set; } = "AutoPost";

    /// <summary>
    /// Gets or sets the optional SMTP username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the optional SMTP password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether TLS should be used.
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}
