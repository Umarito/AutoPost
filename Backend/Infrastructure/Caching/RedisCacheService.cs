using System.Text.Json;
using Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Caching;

/// <summary>
/// Implements the application cache abstraction on top of <see cref="IDistributedCache"/>.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;

    /// <summary>
    /// Initializes the cache adapter.
    /// </summary>
    /// <param name="distributedCache">Distributed cache implementation backed by Redis.</param>
    public RedisCacheService(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var payload = await _distributedCache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        await _distributedCache.SetStringAsync(
            key,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            ct);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        return _distributedCache.RemoveAsync(key, ct);
    }
}
