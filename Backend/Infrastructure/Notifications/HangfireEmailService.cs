using Application.Abstractions.BackgroundJobs;
using Application.Abstractions.Notifications;

namespace Infrastructure.Notifications;

/// <summary>
/// Queues transactional emails through Hangfire so user-facing commands do not block on SMTP I/O.
/// </summary>
public sealed class HangfireEmailService : IEmailService
{
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;

    /// <summary>
    /// Initializes the queued email service.
    /// </summary>
    /// <param name="backgroundJobScheduler">Hangfire-backed background job scheduler.</param>
    public HangfireEmailService(IBackgroundJobScheduler backgroundJobScheduler)
    {
        _backgroundJobScheduler = backgroundJobScheduler;
    }

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _backgroundJobScheduler.Enqueue<SendEmailJob>(job => job.SendAsync(message), "default");
        return Task.CompletedTask;
    }
}
