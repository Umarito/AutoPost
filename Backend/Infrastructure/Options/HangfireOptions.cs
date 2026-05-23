using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Defines background processing settings for Hangfire integration.
/// </summary>
public sealed class HangfireOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Gets or sets the Hangfire PostgreSQL connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dashboard path exposed by the Web API.
    /// </summary>
    [Required]
    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>
    /// Gets or sets the number of background workers started by the Hangfire server.
    /// </summary>
    [Range(1, 256)]
    public int WorkerCount { get; set; } = Math.Max(1, Environment.ProcessorCount * 2);

    /// <summary>
    /// Gets or sets the queue names processed by the Hangfire server.
    /// </summary>
    [MinLength(1)]
    public string[] Queues { get; set; } = ["critical", "default", "low"];
}
