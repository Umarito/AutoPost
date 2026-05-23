using Application.Abstractions.Notifications;

namespace Infrastructure.Notifications;

/// <summary>
/// Hangfire job that performs the actual SMTP delivery of queued transactional emails.
/// </summary>
public sealed class SendEmailJob
{
    private readonly SmtpEmailTransport _smtpEmailTransport;

    /// <summary>
    /// Initializes the email delivery job.
    /// </summary>
    /// <param name="smtpEmailTransport">SMTP transport that performs the actual outbound send.</param>
    public SendEmailJob(SmtpEmailTransport smtpEmailTransport)
    {
        _smtpEmailTransport = smtpEmailTransport;
    }

    /// <summary>
    /// Sends the supplied email message through SMTP.
    /// </summary>
    /// <param name="message">Transactional email payload queued by the application layer.</param>
    /// <returns>A task that completes when SMTP delivery finishes.</returns>
    public Task SendAsync(EmailMessage message)
    {
        return _smtpEmailTransport.SendNowAsync(message);
    }
}
