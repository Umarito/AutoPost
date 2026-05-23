using Domain.Entities;

namespace Application.Abstractions.Integrations;

/// <summary>
/// Validates and refreshes platform access tokens before critical outbound operations.
///
/// <para><b>Security role:</b>
/// This abstraction centralizes pre-flight token checks so handlers do not publish,
/// collect insights or send messages with expired or revoked platform credentials.</para>
/// </summary>
public interface IPlatformTokenValidationService
{
    /// <summary>
    /// Ensures that the supplied social account has a valid platform access token
    /// before the next outbound action is attempted.
    /// </summary>
    /// <param name="socialAccount">The connected social account whose credentials should be verified.</param>
    /// <param name="ct">Cancellation token for remote token-validation I/O.</param>
    /// <returns>A normalized validation outcome including whether a refresh was required.</returns>
    Task<PlatformTokenValidationResult> EnsureValidAsync(SocialAccount socialAccount, CancellationToken ct = default);
}
