namespace Application.Abstractions.Caching;

/// <summary>
/// Provides an application-facing abstraction over distributed caching.
///
/// <para><b>Role in the system:</b>
/// This contract lets CQRS handlers cache read-heavy projections such as dashboard
/// summaries, unread counters, workspace usage and notification settings without
/// leaking Redis-specific APIs into the Application layer.</para>
///
/// <para><b>Performance implications:</b>
/// Proper use of this contract reduces repeated database queries for hot read paths,
/// while still allowing Infrastructure to choose the most appropriate backing store
/// and serialization format.</para>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    /// <typeparam name="T">The expected deserialized payload type.</typeparam>
    /// <param name="key">Stable cache key that uniquely identifies the projection.</param>
    /// <param name="ct">Cancellation token for the underlying I/O operation.</param>
    /// <returns>The cached value if present; otherwise <c>null</c>.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores a value in the distributed cache for the supplied duration.
    /// </summary>
    /// <typeparam name="T">The payload type to serialize.</typeparam>
    /// <param name="key">Stable cache key that uniquely identifies the projection.</param>
    /// <param name="value">The value that should be serialized and cached.</param>
    /// <param name="ttl">How long the value should remain valid before expiring.</param>
    /// <param name="ct">Cancellation token for the underlying I/O operation.</param>
    /// <returns>A task that completes when the value has been persisted to the cache store.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Removes a cached value when a write-side command invalidates a previously cached projection.
    /// </summary>
    /// <param name="key">Stable cache key to invalidate.</param>
    /// <param name="ct">Cancellation token for the underlying I/O operation.</param>
    /// <returns>A task that completes when the cache entry has been removed.</returns>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
