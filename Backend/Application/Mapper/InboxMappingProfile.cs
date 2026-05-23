using Application.DTOs.Inbox;
using Application.DTOs.Workspace;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Inbox aggregate (Conversations, Messages, Assignments).
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>InboxConversation → ConversationSummaryDto</c> — Sidebar list with platform,
/// external user, last message preview, unread count, and assignee name.</item>
/// <item><c>InboxConversation → ConversationDetailDto</c> — Full thread with assignee
/// details mapped as MemberDto and message list.</item>
/// <item><c>InboxMessage → MessageDto</c> — Direction/ContentType/DeliveryStatus enums → strings.
/// SentByName from the User navigation (nullable for inbound messages).</item>
/// <item><c>ConversationAssignment → ConversationAssignmentDto</c> — Flattens assignee
/// and assigner names from User navigations.</item>
/// </list>
/// </para>
///
/// <para><b>Assignment navigation:</b>
/// InboxConversation has an optional Assignment navigation. For ConversationSummaryDto,
/// we extract just the assignee's display name. For ConversationDetailDto, we map the
/// full assignee as a MemberDto (reusing WorkspaceMappingProfile's WorkspaceMember map
/// would require the member entity — so we map from User directly here).</para>
/// </summary>
public class InboxMappingProfile : Profile
{
    public InboxMappingProfile()
    {
        // ── InboxConversation → ConversationSummaryDto ─────────────────────
        // Platform/Type/Status → string. AssigneeName from Assignment navigation.
        CreateMap<InboxConversation, ConversationSummaryDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.AssigneeName, o => o.MapFrom(s =>
                s.Assignment != null ? s.Assignment.AssignedTo.DisplayName : null));

        CreateMap<InboxConversation, ConversationSearchResultDto>()
            .ForMember(d => d.ConversationId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Highlight, o => o.Ignore())
            .ForMember(d => d.Snippet, o => o.MapFrom(s => s.LastMessagePreview));

        // ── InboxConversation → ConversationDetailDto ──────────────────────
        // Full thread: Platform/Type/Status → string. Assignee → MemberDto (nullable).
        // Messages list is automatically mapped via InboxMessage → MessageDto below.
        CreateMap<InboxConversation, ConversationDetailDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Assignee, o => o.MapFrom(s =>
                s.Assignment != null ? s.Assignment.AssignedTo : null));

        // ── ApplicationUser → MemberDto (for inbox assignee) ───────────────
        // Lightweight map used when we have a User entity but not a WorkspaceMember.
        // Role and Status are not available from User alone — defaults to empty strings.
        CreateMap<ApplicationUser, MemberDto>()
            .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.Email!))
            .ForMember(d => d.Role, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.JoinedAt, o => o.Ignore());

        // ── InboxMessage → MessageDto ──────────────────────────────────────
        // Direction/ContentType → string. DeliveryStatus → string (nullable).
        // SentByName from User navigation (null for inbound messages).
        CreateMap<InboxMessage, MessageDto>()
            .ForMember(d => d.Direction, o => o.MapFrom(s => s.Direction.ToString()))
            .ForMember(d => d.ContentType, o => o.MapFrom(s => s.ContentType.ToString()))
            .ForMember(d => d.SentByName, o => o.MapFrom(s =>
                s.SentBy != null ? s.SentBy.DisplayName : null))
            .ForMember(d => d.DeliveryStatus, o => o.MapFrom(s =>
                s.DeliveryStatus.HasValue ? s.DeliveryStatus.Value.ToString() : null));

        // ── ConversationAssignment → ConversationAssignmentDto ─────────────
        // Flattens AssignedTo and AssignedBy user navigations to display names.
        CreateMap<ConversationAssignment, ConversationAssignmentDto>()
            .ForMember(d => d.AssignedToName, o => o.MapFrom(s => s.AssignedTo.DisplayName))
            .ForMember(d => d.AssignedByName, o => o.MapFrom(s =>
                s.AssignedBy != null ? s.AssignedBy.DisplayName : null));
    }
}
