using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Resolves platform-specific publishers at runtime.
/// </summary>
public interface IPlatformPublisherFactory
{
    /// <summary>
    /// Resolves the publisher responsible for the supplied platform.
    /// </summary>
    /// <param name="platform">The target social platform to publish to.</param>
    /// <returns>The platform-specific publisher implementation.</returns>
    IPlatformPublisher Create(Platform platform);
}
