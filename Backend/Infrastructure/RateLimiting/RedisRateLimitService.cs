using Application.Abstractions.RateLimiting;
using Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.RateLimiting;

/// <summary>
/// Implements distributed rate limiting on top of Redis atomic counters.
/// </summary>
public sealed class RedisRateLimitService : IRedisRateLimitService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisOptions _redisOptions;

    /// <summary>
    /// Initializes the Redis-backed rate limit service.
    /// </summary>
    /// <param name="connectionMultiplexer">The shared Redis connection multiplexer.</param>
    /// <param name="redisOptions">The configured Redis options.</param>
    public RedisRateLimitService(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> redisOptions)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _redisOptions = redisOptions.Value;
    }

    /// <inheritdoc />
    public async Task<RateLimitDecision> ConsumeAsync(
        string scope,
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var redisKey = $"{_redisOptions.RateLimitKeyPrefix}:{scope}:{key}";

        var newCount = await database.StringIncrementAsync(redisKey);
        if (newCount == 1)
        {
            await database.KeyExpireAsync(redisKey, window);
        }

        var ttl = await database.KeyTimeToLiveAsync(redisKey) ?? window;
        var remaining = Math.Max(0, permitLimit - (int)newCount);
        var isAllowed = newCount <= permitLimit;

        return new RateLimitDecision(isAllowed, remaining, isAllowed ? TimeSpan.Zero : ttl);
    }

    /// <inheritdoc />
    public Task ResetAsync(string scope, string key, CancellationToken ct = default)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var redisKey = $"{_redisOptions.RateLimitKeyPrefix}:{scope}:{key}";
        return database.KeyDeleteAsync(redisKey);
    }
}
