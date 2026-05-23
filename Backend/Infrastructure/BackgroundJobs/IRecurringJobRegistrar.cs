namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Registers recurring infrastructure-level background jobs during application startup.
/// </summary>
public interface IRecurringJobRegistrar
{
    /// <summary>
    /// Registers or refreshes all recurring jobs required by the currently deployed application.
    /// </summary>
    void RegisterRecurringJobs();
}
