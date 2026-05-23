using Application.Abstractions.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using System.Linq.Expressions;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Implements the application background job abstraction on top of Hangfire.
/// </summary>
public sealed class HangfireBackgroundJobScheduler : IBackgroundJobScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;

    /// <summary>
    /// Initializes the scheduler wrapper with the Hangfire clients used to create jobs.
    /// </summary>
    /// <param name="backgroundJobClient">The Hangfire background job client for one-off work.</param>
    /// <param name="recurringJobManager">The Hangfire recurring job manager for CRON jobs.</param>
    public HangfireBackgroundJobScheduler(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
    }

    /// <inheritdoc />
    public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queue = "default")
        => _backgroundJobClient.Create(Job.FromExpression(methodCall), new EnqueuedState(queue));

    /// <inheritdoc />
    public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay, string queue = "default")
        => _backgroundJobClient.Create(Job.FromExpression(methodCall), new ScheduledState(delay));

    /// <inheritdoc />
    public void AddOrUpdateRecurring<TJob>(
        string recurringJobId,
        Expression<Func<TJob, Task>> methodCall,
        string cronExpression,
        string queue = "default")
    {
#pragma warning disable CS0618
        _recurringJobManager.AddOrUpdate(
            recurringJobId,
            Job.FromExpression(methodCall),
            cronExpression,
            new RecurringJobOptions
            {
                QueueName = queue
            });
#pragma warning restore CS0618
    }

    /// <inheritdoc />
    public bool Delete(string jobId) => _backgroundJobClient.Delete(jobId);
}
