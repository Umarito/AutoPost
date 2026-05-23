using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Defines Redis connection and namespacing settings used by caching and distributed rate limiting.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key prefix used for distributed cache namespacing.
    /// </summary>
    [Required]
    public string InstanceName { get; set; } = "AutoPost:";

    /// <summary>
    /// Gets or sets the key prefix used for distributed rate-limit counters.
    /// </summary>
    [Required]
    public string RateLimitKeyPrefix { get; set; } = "rate-limit";
}
