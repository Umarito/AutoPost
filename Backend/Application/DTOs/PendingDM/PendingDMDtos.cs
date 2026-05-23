namespace Application.DTOs.PendingDM;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  PENDING DM QUEUE DTOs — Deferred DM Management & Monitoring               ║
// ║  TRD Stage 4: Automation (Extended)                                        ║
// ║  Used by: Automation dashboard, Hangfire polling job, admin monitoring      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// Pending DM queue entry for display in the automation monitoring dashboard.
///
/// <para><b>Role in the system:</b>
/// When an automation rule triggers a SendDM action but the target user has a private account
/// (or another blocking condition exists), the message is queued in PendingDMQueue.
/// A Hangfire job polls every 30 minutes to check if the condition has been resolved
/// (e.g., the user followed back). This DTO lets the team monitor the queue: see which
/// messages are waiting, how many check attempts have been made, and when they expire.</para>
///
/// <para><b>Where it's used:</b>
/// Rendered in the "Pending Messages" tab of the automation rule detail page,
/// and in a global queue monitor accessible to workspace admins.</para>
/// </summary>
/// <param name="Id">The queue entry's unique identifier.</param>
/// <param name="AutomationRuleName">Name of the rule that generated this DM (for context).</param>
/// <param name="Platform">Platform as string: "Instagram", "Twitter", etc.</param>
/// <param name="AccountDisplayName">Social account display name the DM will be sent from.</param>
/// <param name="ExternalUserId">Platform-side ID of the target user who should receive the DM.</param>
/// <param name="ExternalUserName">Target user's display name on the platform, or null.</param>
/// <param name="ResolvedMessageText">The final message text that will be sent (variables already substituted).</param>
/// <param name="Reason">Why the DM was deferred: "PrivateAccount", "RateLimited", "AccountNotFollowing".</param>
/// <param name="Status">Current status: "Waiting", "Sent", "Expired", "Cancelled".</param>
/// <param name="TriggeredAt">UTC timestamp when the original automation trigger fired.</param>
/// <param name="ExpiresAt">UTC timestamp after which the system stops trying to send.</param>
/// <param name="CheckAttemptCount">Number of times the Hangfire job has checked this entry's send conditions.</param>
/// <param name="LastCheckedAt">UTC timestamp of the most recent check attempt, or null if never checked.</param>
public record PendingDMQueueDto(
    Guid Id,
    string AutomationRuleName,
    string Platform,
    string AccountDisplayName,
    string ExternalUserId,
    string? ExternalUserName,
    string ResolvedMessageText,
    string Reason,
    string Status,
    DateTime TriggeredAt,
    DateTime ExpiresAt,
    int CheckAttemptCount,
    DateTime? LastCheckedAt
);

/// <summary>
/// Compact pending DM summary for inline display in automation rule statistics.
///
/// <para><b>Why a separate summary exists:</b>
/// The full <see cref="PendingDMQueueDto"/> includes the resolved message text (potentially long).
/// This summary provides just the target user and status for aggregated statistics views.</para>
/// </summary>
/// <param name="Id">The queue entry's unique identifier.</param>
/// <param name="ExternalUserName">Target user's display name, or null.</param>
/// <param name="Status">Current status as string.</param>
/// <param name="TriggeredAt">UTC timestamp of the original trigger.</param>
/// <param name="ExpiresAt">UTC expiration timestamp.</param>
public record PendingDMSummaryDto(
    Guid Id,
    string? ExternalUserName,
    string Status,
    DateTime TriggeredAt,
    DateTime ExpiresAt
);
