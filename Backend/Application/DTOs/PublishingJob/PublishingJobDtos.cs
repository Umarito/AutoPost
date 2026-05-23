namespace Application.DTOs.PublishingJob;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  PUBLISHING JOB DTOs — Publishing Attempt History & Debugging              ║
// ║  TRD Stage 2: Content & Publications                                       ║
// ║  Used by: PostDetailDto publishing history, admin debugging tools           ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// A single publishing attempt record for display in the post detail view.
///
/// <para><b>Role in the system:</b>
/// Every time the Hangfire publisher tries to push content to a platform API, it creates a
/// PublishingJob record. If the first attempt fails (e.g., rate limit 429), it retries with
/// exponential backoff. This DTO exposes each attempt's timing, outcome, and API response
/// so the team can diagnose publishing failures without checking server logs.</para>
///
/// <para><b>Where it's used:</b>
/// Rendered in the "Publishing History" accordion on the post detail page. Each PostTarget
/// shows its attempts as a chronological timeline: Attempt 1 → Failed (429), Attempt 2 → Success.</para>
///
/// <para><b>Security:</b>
/// RawApiResponse may contain platform-internal data. The service layer should sanitize or
/// truncate it before exposing to non-admin users. ErrorDetails are developer-facing.</para>
/// </summary>
/// <param name="Id">The publishing job's unique identifier.</param>
/// <param name="PostTargetId">The PostTarget this attempt belongs to (links to platform + account).</param>
/// <param name="AttemptNumber">Sequence number of this attempt (1 = first try, 2+ = retries).</param>
/// <param name="StartedAt">UTC timestamp when this attempt started (before the API call).</param>
/// <param name="CompletedAt">UTC timestamp when this attempt finished, or null if still in progress.</param>
/// <param name="DurationMs">Time taken for this attempt in milliseconds (CompletedAt - StartedAt), or null if in progress.</param>
/// <param name="Outcome">Attempt result as string: "Success", "Failed", "TimedOut", "Retrying".</param>
/// <param name="ErrorDetails">Technical error description if the attempt failed, or null on success. May include HTTP status codes.</param>
/// <param name="RawApiResponse">Truncated raw API response from the platform (for debugging), or null if not captured.</param>
/// <param name="SchedulerJobId">The Hangfire job ID for correlation with infrastructure logs, or null.</param>
public record PublishingJobDto(
    Guid Id,
    Guid PostTargetId,
    int AttemptNumber,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long? DurationMs,
    string Outcome,
    string? ErrorDetails,
    string? RawApiResponse,
    string? SchedulerJobId
);

/// <summary>
/// Compact publishing attempt for inline display within PostTargetDto.
///
/// <para><b>Why a separate summary exists:</b>
/// The full <see cref="PublishingJobDto"/> includes RawApiResponse which can be large (kilobytes).
/// This summary provides just the essential timing and outcome for the attempt timeline
/// without the heavyweight API response data.</para>
/// </summary>
/// <param name="AttemptNumber">Sequence number of this attempt.</param>
/// <param name="StartedAt">UTC timestamp when this attempt started.</param>
/// <param name="Outcome">Attempt result: "Success", "Failed", "TimedOut", "Retrying".</param>
/// <param name="ErrorDetails">Brief error description, or null on success.</param>
public record PublishingJobSummaryDto(
    int AttemptNumber,
    DateTime StartedAt,
    string Outcome,
    string? ErrorDetails
);
