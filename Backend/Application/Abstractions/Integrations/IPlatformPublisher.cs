using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Defines the outbound publishing contract for a single social platform.
///
/// <para><b>Role in the system:</b>
/// Publishing handlers use this abstraction to send prepared post payloads to the
/// correct platform API without depending on transport details, retries, token refresh
/// logic or provider-specific request shapes.</para>
/// </summary>
public interface IPlatformPublisher
{
    /// <summary>
    /// Gets the platform supported by this publisher implementation.
    /// </summary>
    Platform Platform { get; }

    /// <summary>
    /// Publishes prepared content to the remote platform.
    /// </summary>
    /// <param name="request">Normalized publishing payload prepared by the Application layer.</param>
    /// <param name="ct">Cancellation token for outbound network I/O.</param>
    /// <returns>Normalized information about the remote publication attempt.</returns>
    Task<PlatformPublishResult> PublishAsync(PlatformPublishRequest request, CancellationToken ct = default);
}
