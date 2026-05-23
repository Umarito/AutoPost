using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Represents a lightweight recurring heartbeat job used to verify scheduler wiring during Phase 0.
/// </summary>
public sealed class InfrastructureHeartbeatJob
{
    private readonly ILogger<InfrastructureHeartbeatJob> _logger;

    /// <summary>
    /// Initializes the heartbeat job with a logger.
    /// </summary>
    /// <param name="logger">The logger used to emit the heartbeat entry.</param>
    public InfrastructureHeartbeatJob(ILogger<InfrastructureHeartbeatJob> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the heartbeat and writes a structured log entry.
    /// </summary>
    /// <returns>A completed task after the heartbeat entry is written.</returns>
    public Task ExecuteAsync()
    {
        _logger.LogInformation("Infrastructure heartbeat job executed at {ExecutedAtUtc}.", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
