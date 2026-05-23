namespace Application.Abstractions.RateLimiting;

/// <summary>
/// Evaluates distributed rate limits backed by a shared store such as Redis.
///
/// <para>
/// This contract is used for security-sensitive flows where in-memory
/// per-instance rate limiting is not sufficient, such as login brute-force
/// protection in a multi-instance deployment.
/// </para>
/// </summary>
public interface IRedisRateLimitService
{
    /// <summary>
    /// Consumes one permit from the distributed rate limit bucket associated with the given key.
    /// </summary>
    /// <param name="scope">The logical policy scope, for example <c>auth-login</c>.</param>
    /// <param name="key">The caller-specific key, such as IP address or email hash.</param>
    /// <param name="permitLimit">The maximum number of actions allowed in the window.</param>
    /// <param name="window">The rolling time window being enforced.</param>
    /// <param name="ct">The cancellation token for the async operation.</param>
    /// <returns>A decision object describing whether the action is allowed.</returns>
    Task<RateLimitDecision> ConsumeAsync(
        string scope,
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken ct = default);

    /// <summary>
    /// Resets the distributed counter associated with a specific scope/key pair.
    /// </summary>
    /// <param name="scope">Logical rate-limit scope such as <c>auth</c> or <c>login-email</c>.</param>
    /// <param name="key">Concrete subject key whose counter should be removed.</param>
    /// <param name="ct">Cancellation token for the underlying Redis I/O.</param>
    /// <returns>A task that completes when the counter has been deleted.</returns>
    Task ResetAsync(string scope, string key, CancellationToken ct = default);
}
