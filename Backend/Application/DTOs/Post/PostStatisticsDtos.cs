namespace Application.DTOs.Post;

/// <summary>
/// Aggregated operational statistics for posts within a selected time range.
///
/// <para><b>Role in the system:</b>
/// This DTO supports content dashboards and reporting screens that need quick counts
/// of draft, scheduled, published and failed posts without loading the full post list.</para>
/// </summary>
/// <param name="DraftCount">Number of posts currently in draft state.</param>
/// <param name="ScheduledCount">Number of posts waiting to be published.</param>
/// <param name="PublishedCount">Number of posts that completed successfully.</param>
/// <param name="FailedCount">Number of posts that fully failed or partially failed.</param>
/// <param name="TotalTargetCount">Total number of platform targets represented by the filtered posts.</param>
public record PostStatisticsDto(
    int DraftCount,
    int ScheduledCount,
    int PublishedCount,
    int FailedCount,
    int TotalTargetCount);
