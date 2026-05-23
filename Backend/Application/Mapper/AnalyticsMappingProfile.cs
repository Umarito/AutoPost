using Application.DTOs.Analytics;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Analytics aggregate (PostAnalyticsSnapshot, SocialAccountInsight).
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>PostAnalyticsSnapshot → PostAnalyticsSnapshotDto</c> — Direct 1:1 mapping
/// for the timeline chart data points.</item>
/// <item><c>SocialAccountInsight → InsightDto</c> — Maps ProfileReach → Reach and
/// ProfileImpressions → Impressions (entity uses "Profile" prefix, DTO doesn't).</item>
/// </list>
/// </para>
///
/// <para><b>What is NOT mapped here:</b>
/// <c>DashboardSummaryDto</c>, <c>PostAnalyticsDto</c>, and <c>PlatformAnalyticsDto</c>
/// are composed manually by the AnalyticsService from aggregated query results.
/// They don't have a 1:1 entity correspondence — they aggregate data from multiple
/// repositories (Posts, SocialAccountInsights, InboxConversations, AutomationLogs).</para>
/// </summary>
public class AnalyticsMappingProfile : Profile
{
    public AnalyticsMappingProfile()
    {
        // ── PostAnalyticsSnapshot → PostAnalyticsSnapshotDto ───────────────
        // Direct 1:1: RecordedAt, Views, Likes, Comments, Shares all match by name.
        CreateMap<PostAnalyticsSnapshot, PostAnalyticsSnapshotDto>();

        // ── SocialAccountInsight → InsightDto ──────────────────────────────
        // ProfileReach → Reach, ProfileImpressions → Impressions (rename for API clarity).
        CreateMap<SocialAccountInsight, InsightDto>()
            .ForMember(d => d.Reach, o => o.MapFrom(s => s.ProfileReach))
            .ForMember(d => d.Impressions, o => o.MapFrom(s => s.ProfileImpressions));
    }
}
