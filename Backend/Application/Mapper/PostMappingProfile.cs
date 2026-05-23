using Application.DTOs.Post;
using Application.DTOs.Video;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Post aggregate — the most complex mapping in the system.
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>Post → PostSummaryDto</c> — Flattens value objects (Content, Schedule),
/// computes target success/failure counts from the Targets collection.</item>
/// <item><c>Post → PostDetailDto</c> — Full post with nested Video summary and PostTarget list.</item>
/// <item><c>Post → PostCalendarDto</c> — Minimal calendar representation with platform list.</item>
/// <item><c>PostTarget → PostTargetDto</c> — Flattens the Result value object and SocialAccount navigation.</item>
/// </list>
/// </para>
///
/// <para><b>Value Object flattening:</b>
/// Post has two value objects: <c>PostContent</c> (Title, Description, Tags, Visibility) and
/// <c>Schedule</c> (ScheduledAt, TimeZoneId). These are flattened into the DTOs using explicit
/// .ForMember() because AutoMapper's convention-based flattening uses "ContentTitle" not "Title".</para>
///
/// <para><b>Computed properties:</b>
/// TotalTargets, SuccessTargets, FailedTargets are computed from the Targets collection using
/// LINQ Count() with status filters. These work both in-memory and with EF Core ProjectTo.</para>
/// </summary>
public class PostMappingProfile : Profile
{
    public PostMappingProfile()
    {
        // ── Post → PostSummaryDto ──────────────────────────────────────────
        // Flattens Content.Title for display, Schedule.ScheduledAt for calendar.
        // Target counts computed via LINQ over the Targets collection.
        // ThumbnailUrl pulled from Video navigation (nullable).
        CreateMap<Post, PostSummaryDto>()
            .ForMember(d => d.Title, o => o.MapFrom(s => s.Content.Title))
            .ForMember(d => d.ThumbnailUrl, o => o.MapFrom(s =>
                s.Video != null ? s.Video.ThumbnailUrl : null))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ScheduledAt, o => o.MapFrom(s => s.Schedule.ScheduledAt))
            .ForMember(d => d.TotalTargets, o => o.MapFrom(s => s.Targets.Count))
            .ForMember(d => d.SuccessTargets, o => o.MapFrom(s =>
                s.Targets.Count(t => t.Status == Domain.Enums.TargetStatus.Published)))
            .ForMember(d => d.FailedTargets, o => o.MapFrom(s =>
                s.Targets.Count(t => t.Status == Domain.Enums.TargetStatus.Failed)));

        // ── Post → PostDetailDto ───────────────────────────────────────────
        // Full post with all value object fields flattened.
        // Video mapped as VideoSummaryDto (reuses the VideoMappingProfile).
        // Targets mapped as List<PostTargetDto> (reuses PostTarget map below).
        CreateMap<Post, PostDetailDto>()
            .ForMember(d => d.Title, o => o.MapFrom(s => s.Content.Title))
            .ForMember(d => d.Description, o => o.MapFrom(s => s.Content.Description))
            .ForMember(d => d.Tags, o => o.MapFrom(s => s.Content.Tags))
            .ForMember(d => d.Visibility, o => o.MapFrom(s => s.Content.Visibility.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ScheduledAt, o => o.MapFrom(s => s.Schedule.ScheduledAt))
            .ForMember(d => d.TimeZoneId, o => o.MapFrom(s => s.Schedule.TimeZoneId));

        // ── Post → PostCalendarDto ─────────────────────────────────────────
        // Minimal view: only title, status, schedule, and platform icon badges.
        // Platforms[] extracted from Targets → SocialAccount.Platform.
        CreateMap<Post, PostCalendarDto>()
            .ForMember(d => d.Title, o => o.MapFrom(s => s.Content.Title))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ScheduledAt, o => o.MapFrom(s => s.Schedule.ScheduledAt))
            .ForMember(d => d.Platforms, o => o.MapFrom(s =>
                s.Targets.Select(t => t.Platform.ToString()).Distinct().ToArray()));

        // ── PostTarget → PostTargetDto ─────────────────────────────────────
        // Flattens the Result value object (ExternalPostUrl, ErrorMessage, AttemptCount).
        // Flattens the SocialAccount navigation (AccountDisplayName, AccountAvatarUrl).
        // Platform enum → string. Status enum → string.
        CreateMap<PostTarget, PostTargetDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.AccountDisplayName, o => o.MapFrom(s => s.SocialAccount.AccountDisplayName))
            .ForMember(d => d.AccountAvatarUrl, o => o.MapFrom(s => s.SocialAccount.AccountAvatarUrl))
            .ForMember(d => d.ExternalPostUrl, o => o.MapFrom(s =>
                s.Result != null ? s.Result.ExternalPostUrl : null))
            .ForMember(d => d.ErrorMessage, o => o.MapFrom(s =>
                s.Result != null ? s.Result.ErrorMessage : null))
            .ForMember(d => d.AttemptCount, o => o.MapFrom(s =>
                s.Result != null ? s.Result.AttemptCount : 0));
    }
}
