using Application.Abstractions.BackgroundJobs;
using Hangfire;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the recurring jobs that are safe and required during the Phase 0 infrastructure bootstrap.
/// </summary>
public sealed class InfrastructureRecurringJobRegistrar : IRecurringJobRegistrar
{
    private readonly IBackgroundJobScheduler _backgroundJobScheduler;

    /// <summary>
    /// Initializes the registrar with the background scheduler abstraction.
    /// </summary>
    /// <param name="backgroundJobScheduler">The scheduler used to register recurring jobs.</param>
    public InfrastructureRecurringJobRegistrar(IBackgroundJobScheduler backgroundJobScheduler)
    {
        _backgroundJobScheduler = backgroundJobScheduler;
    }

    /// <inheritdoc />
    public void RegisterRecurringJobs()
    {
        _backgroundJobScheduler.AddOrUpdateRecurring<InfrastructureHeartbeatJob>(
            recurringJobId: "infrastructure-heartbeat",
            methodCall: job => job.ExecuteAsync(),
            cronExpression: Cron.Hourly(),
            queue: "low");
    }
}
