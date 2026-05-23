using System.Linq.Expressions;

namespace Application.Abstractions.BackgroundJobs;

/// <summary>
/// Provides an application-facing abstraction over background job scheduling.
///
/// <para>
/// The Application layer needs to be able to enqueue and schedule durable work
/// without taking a direct dependency on Hangfire. This interface keeps the
/// orchestration logic infrastructure-agnostic while still allowing the outer
/// layer to plug in a persistent scheduler.
/// </para>
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Enqueues a fire-and-forget background job for immediate execution.
    /// </summary>
    /// <typeparam name="TJob">The service type that contains the target method.</typeparam>
    /// <param name="methodCall">The async method expression Hangfire-style schedulers can serialize.</param>
    /// <param name="queue">The logical queue name used for prioritization.</param>
    /// <returns>The scheduler-specific job identifier.</returns>
    string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queue = "default");

    /// <summary>
    /// Schedules a background job to run after the specified delay.
    /// </summary>
    /// <typeparam name="TJob">The service type that contains the target method.</typeparam>
    /// <param name="methodCall">The async method expression to schedule.</param>
    /// <param name="delay">The amount of time to wait before execution.</param>
    /// <param name="queue">The logical queue name used for prioritization.</param>
    /// <returns>The scheduler-specific job identifier.</returns>
    string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay, string queue = "default");

    /// <summary>
    /// Registers or updates a recurring background job.
    /// </summary>
    /// <typeparam name="TJob">The service type that contains the target method.</typeparam>
    /// <param name="recurringJobId">The stable logical identifier of the recurring job.</param>
    /// <param name="methodCall">The async method expression to execute on schedule.</param>
    /// <param name="cronExpression">The CRON expression describing the recurrence cadence.</param>
    /// <param name="queue">The logical queue name used for prioritization.</param>
    void AddOrUpdateRecurring<TJob>(
        string recurringJobId,
        Expression<Func<TJob, Task>> methodCall,
        string cronExpression,
        string queue = "default");

    /// <summary>
    /// Deletes a previously scheduled or enqueued job when the backing scheduler supports it.
    /// </summary>
    /// <param name="jobId">The scheduler-specific job identifier.</param>
    /// <returns><c>true</c> when a job was removed; otherwise <c>false</c>.</returns>
    bool Delete(string jobId);
}
