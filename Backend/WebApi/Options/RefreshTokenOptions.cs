using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Options;

/// <summary>
/// Defines transport settings for refresh tokens stored in secure HTTP cookies.
/// </summary>
public sealed class RefreshTokenOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Gets or sets the cookie name used to store the refresh token.
    /// </summary>
    [Required]
    public string CookieName { get; set; } = "autopost.refresh_token";

    /// <summary>
    /// Gets or sets the refresh token lifetime in days.
    /// </summary>
    [Range(1, 365)]
    public int LifetimeDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the cookie path that should receive the refresh token cookie.
    /// </summary>
    [Required]
    public string CookiePath { get; set; } = "/";

    /// <summary>
    /// Gets or sets the optional cookie domain.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Gets or sets the SameSite mode applied to the refresh token cookie.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.None;

    /// <summary>
    /// Gets or sets the secure policy applied to the refresh token cookie.
    /// </summary>
    public CookieSecurePolicy SecurePolicy { get; set; } = CookieSecurePolicy.Always;

    /// <summary>
    /// Gets or sets a value indicating whether the cookie is inaccessible to JavaScript.
    /// </summary>
    public bool HttpOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the cookie should be treated as essential.
    /// </summary>
    public bool IsEssential { get; set; } = true;
}
