namespace Application.Abstractions.RateLimiting;

/// <summary>
/// Represents the result of a distributed rate-limit check.
///
/// <para>
/// The decision object allows outer layers to return accurate retry metadata
/// without leaking Redis-specific implementation details into the Application layer.
/// </para>
/// </summary>
/// <param name="IsAllowed">Indicates whether the current request or action may proceed.</param>
/// <param name="RemainingPermits">The remaining number of allowed operations in the current window.</param>
/// <param name="RetryAfter">The amount of time the caller should wait before retrying when denied.</param>
public sealed record RateLimitDecision(bool IsAllowed, int RemainingPermits, TimeSpan RetryAfter);
