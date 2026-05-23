using System.ComponentModel.DataAnnotations;

namespace WebApi.Options;

/// <summary>
/// Defines rate-limiting thresholds for public API, authentication and upload traffic.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Gets or sets the general API permit limit per fixed window.
    /// </summary>
    [Range(1, 10000)]
    public int GeneralPermitLimit { get; set; } = 60;

    /// <summary>
    /// Gets or sets the general API fixed window length in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int GeneralWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the authentication permit limit enforced by the distributed Redis limiter.
    /// </summary>
    [Range(1, 1000)]
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets the authentication rate-limit window length in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of upload tokens stored in the bucket.
    /// </summary>
    [Range(1, 10000)]
    public int UploadTokenLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets how many upload tokens are replenished in each period.
    /// </summary>
    [Range(1, 10000)]
    public int UploadTokensPerPeriod { get; set; } = 20;

    /// <summary>
    /// Gets or sets the upload token replenishment period in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int UploadReplenishmentSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the retry-after value returned by the built-in rate limiter.
    /// </summary>
    [Range(1, 3600)]
    public int RejectionRetryAfterSeconds { get; set; } = 60;
}
