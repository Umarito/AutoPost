using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Infrastructure.HealthChecks;

/// <summary>
/// Performs a lightweight Redis health check using the shared connection multiplexer.
/// </summary>
public sealed class RedisConnectionHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    /// <summary>
    /// Initializes the health check with the shared Redis connection.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer used by the application.</param>
    public RedisConnectionHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            var latency = await database.PingAsync();

            return HealthCheckResult.Healthy("Redis connection is healthy.", new Dictionary<string, object>
            {
                ["latency_ms"] = latency.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection check failed.", ex);
        }
    }
}
