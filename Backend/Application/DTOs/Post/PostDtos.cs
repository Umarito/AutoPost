using System.ComponentModel.DataAnnotations;
using Application.DTOs.Video;
using Domain.Enums;

namespace Application.DTOs.Post;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  POST DTOs — Content Creation, Scheduling, Calendar, Platform Validation   ║
// ║  TRD Stage 2: Content & Publications                                       ║
// ║  Endpoints: POST/GET/PUT/DELETE /api/posts, GET .../calendar               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

// ── Request DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for creating a new scheduled post.
///
/// <para><b>What it does:</b>
/// Creates a Post entity with its child PostTargets (one per selected social account).
/// The service also schedules a Hangfire background job that will fire at ScheduledAt
/// to publish the content to all target platforms.</para>
///
/// <para><b>How targeting works:</b>
/// The user selects one or more connected SocialAccount IDs. The service creates a PostTarget
/// for each account, and the publisher job processes them independently — allowing partial
/// success (e.g., YouTube succeeds but Instagram fails).</para>
///
/// <para><b>TRD API:</b> POST /api/posts</para>
/// </summary>
/// <param name="VideoId">The Id of the video from the media library to attach to this post.</param>
/// <param name="Title">The post's title/caption. Required for all platforms.</param>
/// <param name="Description">Extended description text, or null. Used by YouTube and Facebook.</param>
/// <param name="Tags">List of tags/hashtags to include. Stored as JSON array in the database.</param>
/// <param name="Visibility">Content visibility: Public, Unlisted, or Private. Default: Public.</param>
/// <param name="CustomThumbnailUrl">URL to a custom thumbnail image (overrides auto-generated), or null.</param>
/// <param name="ScheduledAt">UTC timestamp when the post should be published. Must be in the future.</param>
/// <param name="TimeZoneId">IANA timezone (e.g., "Europe/Moscow") for displaying the schedule in the user's local time.</param>
/// <param name="TargetAccountIds">List of SocialAccount IDs to publish to. At least one required.</param>
/// <param name="PlatformSettingsJson">Platform-specific settings as JSON (e.g., YouTube category, Instagram location). Optional.</param>
public record CreatePostRequest(
    [Required] Guid VideoId,
    [Required, MaxLength(500)] string Title,
    [MaxLength(5000)] string? Description,
    List<string>? Tags,
    Visibility Visibility = Visibility.Public,
    [MaxLength(2048)] string? CustomThumbnailUrl = null,
    [Required] DateTime ScheduledAt = default,
    [Required, MaxLength(100)] string TimeZoneId = "UTC",
    [Required, MinLength(1)] List<Guid> TargetAccountIds = default!,
    string? PlatformSettingsJson = null
);

/// <summary>
/// Payload for updating an existing post.
///
/// <para><b>Restrictions:</b>
/// Only posts with Status == Draft or Scheduled can be updated. Published or Failed posts
/// are immutable. If ScheduledAt is changed, the Hangfire job is rescheduled.</para>
///
/// <para><b>PATCH semantics:</b>
/// All fields are optional — only non-null values are applied to the existing post.</para>
///
/// <para><b>TRD API:</b> PUT /api/posts/{id}</para>
/// </summary>
/// <param name="Title">New title, or null to keep current.</param>
/// <param name="Description">New description, or null to keep current.</param>
/// <param name="Tags">New tags list, or null to keep current.</param>
/// <param name="Visibility">New visibility, or null to keep current.</param>
/// <param name="ScheduledAt">New schedule time (UTC), or null to keep current. Triggers Hangfire job reschedule.</param>
/// <param name="TimeZoneId">New timezone, or null to keep current.</param>
public record UpdatePostRequest(
    [MaxLength(500)] string? Title,
    [MaxLength(5000)] string? Description,
    List<string>? Tags,
    Visibility? Visibility,
    DateTime? ScheduledAt,
    [MaxLength(100)] string? TimeZoneId
);

/// <summary>
/// Filter parameters for listing posts.
///
/// <para><b>What it does:</b>
/// Enables the content calendar and list views to filter by status, platform, date range,
/// and text search. All filters are optional — an empty request returns all posts.</para>
///
/// <para><b>TRD API:</b> GET /api/posts?status=Scheduled&amp;platform=YouTube&amp;from=...&amp;to=...</para>
/// </summary>
/// <param name="Status">Filter by post status: "Draft", "Scheduled", "Published", "Failed", or null for all.</param>
/// <param name="Platform">Filter by target platform: "YouTube", "Instagram", etc., or null for all.</param>
/// <param name="From">Start of date range (inclusive, UTC), or null for no lower bound.</param>
/// <param name="To">End of date range (inclusive, UTC), or null for no upper bound.</param>
/// <param name="Search">Free-text search against the post title, or null for no search.</param>
public record PostFilterRequest(
    string? Status,
    string? Platform,
    DateTime? From,
    DateTime? To,
    string? Search
);

// ── Response DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Compact post representation for list views and content calendar.
///
/// <para><b>What it contains:</b>
/// Essential fields for rendering a post card: title, thumbnail, status, schedule,
/// and a breakdown of target outcomes (total / success / failed).</para>
///
/// <para><b>TRD API:</b> GET /api/posts</para>
/// </summary>
/// <param name="Id">The post's unique identifier.</param>
/// <param name="Title">The post's title/caption.</param>
/// <param name="ThumbnailUrl">Thumbnail URL from the attached video, or null.</param>
/// <param name="Status">Lifecycle status as string: "Draft", "Scheduled", "Publishing", "Published", "PartiallyPublished", "Failed".</param>
/// <param name="ScheduledAt">UTC timestamp when the post is/was scheduled for publication.</param>
/// <param name="CompletedAt">UTC timestamp when publishing completed (all targets finished), or null if still pending.</param>
/// <param name="TotalTargets">Number of social accounts this post targets.</param>
/// <param name="SuccessTargets">Number of targets that published successfully.</param>
/// <param name="FailedTargets">Number of targets that failed to publish.</param>
public record PostSummaryDto(
    Guid Id,
    string Title,
    string? ThumbnailUrl,
    string Status,
    DateTime ScheduledAt,
    DateTime? CompletedAt,
    int TotalTargets,
    int SuccessTargets,
    int FailedTargets
);

/// <summary>
/// Complete post representation with all details for the post detail/edit page.
///
/// <para><b>What it adds over <see cref="PostSummaryDto"/>:</b>
/// Full content (description, tags, visibility), timezone info, attached video summary,
/// and per-platform target statuses with error messages and attempt counts.</para>
///
/// <para><b>TRD API:</b> GET /api/posts/{id}</para>
/// </summary>
/// <param name="Id">The post's unique identifier.</param>
/// <param name="Title">The post's title/caption.</param>
/// <param name="Description">Extended description text, or null.</param>
/// <param name="Tags">List of tags/hashtags.</param>
/// <param name="Visibility">Content visibility as string: "Public", "Unlisted", "Private".</param>
/// <param name="Status">Lifecycle status as string.</param>
/// <param name="ScheduledAt">UTC scheduled publication time.</param>
/// <param name="TimeZoneId">IANA timezone for local time display.</param>
/// <param name="CompletedAt">UTC completion time, or null.</param>
/// <param name="Video">Summary of the attached video, or null if no video.</param>
/// <param name="Targets">Per-platform publishing status for each target account.</param>
/// <param name="CreatedAt">UTC timestamp when the post was created.</param>
public record PostDetailDto(
    Guid Id,
    string Title,
    string? Description,
    List<string> Tags,
    string Visibility,
    string Status,
    DateTime ScheduledAt,
    string TimeZoneId,
    DateTime? CompletedAt,
    VideoSummaryDto? Video,
    List<PostTargetDto> Targets,
    DateTime CreatedAt
);

/// <summary>
/// Per-platform publishing status for a single target account within a post.
///
/// <para><b>What it represents:</b>
/// Each PostTarget tracks the independent publishing outcome for one social account.
/// A post targeting YouTube + Instagram has two PostTargets, each with its own status.</para>
/// </summary>
/// <param name="Id">The post target's unique identifier.</param>
/// <param name="Platform">Target platform as string: "YouTube", "Instagram", etc.</param>
/// <param name="AccountDisplayName">Display name of the connected social account.</param>
/// <param name="AccountAvatarUrl">Avatar URL of the social account, or null.</param>
/// <param name="Status">Publishing status: "Pending", "Publishing", "Published", "Failed".</param>
/// <param name="ExternalPostUrl">URL to the published post on the platform (e.g., youtube.com/watch?v=...), or null if not yet published.</param>
/// <param name="ErrorMessage">Error description if publishing failed, or null on success.</param>
/// <param name="AttemptCount">Number of publishing attempts made (1 = first try, 2+ = retries).</param>
public record PostTargetDto(
    Guid Id,
    string Platform,
    string AccountDisplayName,
    string? AccountAvatarUrl,
    string Status,
    string? ExternalPostUrl,
    string? ErrorMessage,
    int AttemptCount
);

/// <summary>
/// Minimal post representation optimized for the calendar view.
///
/// <para><b>What it contains:</b>
/// Only the fields needed to render a calendar event: title, status, scheduled time,
/// and which platforms are targeted (as icon badges on the calendar event).</para>
///
/// <para><b>TRD API:</b> GET /api/posts/calendar?from=...&amp;to=...</para>
/// </summary>
/// <param name="Id">The post's unique identifier (for linking to detail page).</param>
/// <param name="Title">The post's title (shown as the calendar event label).</param>
/// <param name="Status">Lifecycle status as string (for color-coding the calendar event).</param>
/// <param name="ScheduledAt">UTC scheduled time (determines which day/time slot on the calendar).</param>
/// <param name="Platforms">Array of platform names targeted by this post (for rendering platform icon badges).</param>
public record PostCalendarDto(
    Guid Id,
    string Title,
    string Status,
    DateTime ScheduledAt,
    string[] Platforms
);

/// <summary>
/// Platform compatibility check result for a video before publishing.
///
/// <para><b>What it does:</b>
/// Each platform has specific requirements (max duration, min resolution, allowed codecs).
/// This DTO reports whether a video is compatible with a platform, along with any
/// warnings (non-blocking) and errors (blocking).</para>
///
/// <para><b>TRD API:</b> POST /api/posts/validate</para>
/// </summary>
/// <param name="Platform">The platform being validated against: "YouTube", "Instagram", etc.</param>
/// <param name="IsCompatible">Whether the video meets all mandatory requirements for this platform.</param>
/// <param name="Warnings">Non-blocking issues (e.g., "Video is longer than recommended 60s for Reels").</param>
/// <param name="Errors">Blocking issues that prevent publishing (e.g., "Resolution below minimum 720p").</param>
public record PlatformValidationDto(
    string Platform,
    bool IsCompatible,
    List<string> Warnings,
    List<string> Errors
);
