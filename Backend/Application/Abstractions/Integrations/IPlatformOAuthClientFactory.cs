using Domain.Enums;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Resolves outbound OAuth/API clients for supported social platforms.
///
/// <para>
/// The factory keeps the Application layer unaware of named/typed HttpClient
/// registrations, provider SDK choices and resilience plumbing. Callers ask
/// for a platform and receive the correctly configured client abstraction.
/// </para>
/// </summary>
public interface IPlatformOAuthClientFactory
{
    /// <summary>
    /// Creates or resolves a configured client for the specified platform.
    /// </summary>
    /// <param name="platform">The target platform whose OAuth/API endpoints will be called.</param>
    /// <returns>A configured platform client.</returns>
    IPlatformOAuthClient CreateClient(Platform platform);
}
