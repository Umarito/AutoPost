using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Represents a configured outbound HTTP client for a specific social platform.
///
/// <para>
/// The platform-specific client carries the correct base address, headers and
/// resilience policies required to communicate with an OAuth provider or a
/// platform API endpoint. The Application layer only depends on this contract,
/// while Infrastructure decides how the underlying <see cref="HttpClient"/> is built.
/// </para>
/// </summary>
public interface IPlatformOAuthClient
{
    /// <summary>
    /// Gets the platform this client instance is responsible for.
    /// </summary>
    Platform Platform { get; }

    /// <summary>
    /// Gets the configured HTTP client used for outbound requests.
    /// </summary>
    HttpClient HttpClient { get; }
}
