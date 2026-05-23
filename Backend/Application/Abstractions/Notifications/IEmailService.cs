namespace Application.Abstractions.Notifications;

/// <summary>
/// Sends transactional email messages required by the MVP.
///
/// <para><b>Typical use cases:</b>
/// Email confirmation, workspace invitations, security alerts, post failure alerts
/// and notification preference-driven outbound notifications.</para>
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email message.
    /// </summary>
    /// <param name="message">Normalized email payload prepared by the Application layer.</param>
    /// <param name="ct">Cancellation token for outbound provider I/O.</param>
    /// <returns>A task that completes when the provider accepts the message for delivery.</returns>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
