using Application.DTOs.PublishingJob;
using Application.DTOs.Webhook;
using Application.DTOs.PendingDM;
using Application.DTOs.Notification;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for infrastructure-level entities: PublishingJob, WebhookEvent,
/// PendingDMQueue, and NotificationPreference.
///
/// <para><b>Why grouped together:</b>
/// These entities are operational/infrastructure concerns rather than core domain aggregates.
/// They share the pattern of simple entity→DTO mappings with enum→string conversions,
/// so grouping them reduces file proliferation without sacrificing clarity.</para>
/// </summary>
public class InfrastructureMappingProfile : Profile
{
    public InfrastructureMappingProfile()
    {
        // ═══════════════════════════════════════════════════════════════════
        //  PublishingJob
        // ═══════════════════════════════════════════════════════════════════

        // ── PublishingJob → PublishingJobDto ────────────────────────────────
        // Outcome enum → string. DurationMs computed from StartedAt/CompletedAt.
        CreateMap<PublishingJob, PublishingJobDto>()
            .ForMember(d => d.Outcome, o => o.MapFrom(s => s.Outcome.ToString()))
            .ForMember(d => d.DurationMs, o => o.MapFrom(s =>
                s.CompletedAt.HasValue
                    ? (long?)(s.CompletedAt.Value - s.StartedAt).TotalMilliseconds
                    : null));

        // ── PublishingJob → PublishingJobSummaryDto ─────────────────────────
        // Compact version without RawApiResponse.
        CreateMap<PublishingJob, PublishingJobSummaryDto>()
            .ForMember(d => d.Outcome, o => o.MapFrom(s => s.Outcome.ToString()));

        // ═══════════════════════════════════════════════════════════════════
        //  WebhookEvent
        // ═══════════════════════════════════════════════════════════════════

        // ── WebhookEvent → WebhookEventDto ─────────────────────────────────
        // Platform/Status enums → strings. RawPayload excluded (list view).
        CreateMap<WebhookEvent, WebhookEventDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ── WebhookEvent → WebhookEventDetailDto ───────────────────────────
        // Full detail including RawPayload and Signature (admin-only view).
        CreateMap<WebhookEvent, WebhookEventDetailDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ═══════════════════════════════════════════════════════════════════
        //  PendingDMQueue
        // ═══════════════════════════════════════════════════════════════════

        // ── PendingDMQueue → PendingDMQueueDto ─────────────────────────────
        // Reason/Status enums → strings. AutomationRuleName and Platform/AccountDisplayName
        // flattened from navigations.
        CreateMap<PendingDMQueue, PendingDMQueueDto>()
            .ForMember(d => d.AutomationRuleName, o => o.MapFrom(s => s.AutomationRule.Name))
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.AccountDisplayName, o => o.MapFrom(s => s.SocialAccount.AccountDisplayName))
            .ForMember(d => d.Reason, o => o.MapFrom(s => s.Reason.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ── PendingDMQueue → PendingDMSummaryDto ───────────────────────────
        // Compact version with just target user and status.
        CreateMap<PendingDMQueue, PendingDMSummaryDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ═══════════════════════════════════════════════════════════════════
        //  NotificationPreference
        // ═══════════════════════════════════════════════════════════════════

        // ── NotificationPreference → NotificationPreferenceDto ─────────────
        // EventType enum → string. EventTypeDescription is not on the entity —
        // it will be populated by the service layer based on a lookup dictionary.
        CreateMap<NotificationPreference, NotificationPreferenceDto>()
            .ForMember(d => d.EventType, o => o.MapFrom(s => s.EventType.ToString()))
            .ForMember(d => d.EventTypeDescription, o => o.Ignore());

        CreateMap<NotificationHistory, NotificationHistoryDto>()
            .ForMember(d => d.EventType, o => o.MapFrom(s => s.EventType.ToString()))
            .ForMember(d => d.Channel, o => o.MapFrom(s => s.Channel.ToString()));
    }
}
