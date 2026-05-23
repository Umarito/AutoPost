using System.Net;
using System.Net.Mail;
using Application.Abstractions.Notifications;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

/// <summary>
/// Sends transactional emails through SMTP.
/// </summary>
public sealed class SmtpEmailTransport
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<SmtpEmailTransport> _logger;

    /// <summary>
    /// Initializes the SMTP transport.
    /// </summary>
    /// <param name="smtpOptions">Validated SMTP configuration.</param>
    /// <param name="logger">Structured logger for delivery diagnostics.</param>
    public SmtpEmailTransport(
        IOptions<SmtpOptions> smtpOptions,
        ILogger<SmtpEmailTransport> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends the email message immediately using SMTP.
    /// </summary>
    /// <param name="message">Normalized application email payload.</param>
    /// <param name="ct">Cancellation token used to cancel the send wait.</param>
    /// <returns>A task that completes when the SMTP provider accepts the message.</returns>
    public async Task SendNowAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        mailMessage.To.Add(message.ToEmail);

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            mailMessage.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.TextBody,
                    null,
                    "text/plain"));
        }

        _logger.LogInformation("Sending SMTP email to {Recipient} with subject {Subject}.", message.ToEmail, message.Subject);
        await client.SendMailAsync(mailMessage).WaitAsync(ct);
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
        {
            client.Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password);
        }

        return client;
    }
}
