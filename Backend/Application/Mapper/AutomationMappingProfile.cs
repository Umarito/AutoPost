using Application.DTOs.Automation;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Automation aggregate (Rules, Conditions, Actions, ExecutionLogs).
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>AutomationRule → AutomationRuleDto</c> — List view with platform and account
/// display name from the SocialAccount navigation.</item>
/// <item><c>AutomationRule → AutomationRuleDetailDto</c> — Full rule with nested
/// SocialAccountDto, conditions, and actions.</item>
/// <item><c>TriggerCondition → ConditionDto</c> — Type/Operator enums → strings.</item>
/// <item><c>AutomationAction → ActionDto</c> — Type enum → string.</item>
/// <item><c>AutomationExecutionLog → ExecutionLogDto</c> — Outcome enum → string.</item>
/// </list>
/// </para>
///
/// <para><b>SocialAccount nesting:</b>
/// AutomationRuleDetailDto embeds a full SocialAccountDto. This reuses the map from
/// SocialAccountMappingProfile. The rule's SocialAccount navigation must be .Include()-d.</para>
/// </summary>
public class AutomationMappingProfile : Profile
{
    public AutomationMappingProfile()
    {
        // ── AutomationRule → AutomationRuleDto ─────────────────────────────
        // List view: Platform/TriggerType → string. AccountDisplayName from navigation.
        CreateMap<AutomationRule, AutomationRuleDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.AccountDisplayName, o => o.MapFrom(s => s.SocialAccount.AccountDisplayName))
            .ForMember(d => d.TriggerType, o => o.MapFrom(s => s.TriggerType.ToString()));

        // ── AutomationRule → AutomationRuleDetailDto ───────────────────────
        // Full rule: includes SocialAccountDto (mapped by SocialAccountMappingProfile),
        // Conditions (mapped below), Actions (mapped below).
        // Platform is extracted from SocialAccount for top-level display.
        CreateMap<AutomationRule, AutomationRuleDetailDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.SocialAccount.Platform.ToString()))
            .ForMember(d => d.TriggerType, o => o.MapFrom(s => s.TriggerType.ToString()));

        // ── TriggerCondition → ConditionDto ────────────────────────────────
        // Type/Operator enums → strings for API serialization.
        CreateMap<TriggerCondition, ConditionDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Operator, o => o.MapFrom(s => s.Operator.ToString()));

        // ── AutomationAction → ActionDto ───────────────────────────────────
        // Type enum → string. All other properties are 1:1 name matches.
        CreateMap<AutomationAction, ActionDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()));

        // ── AutomationExecutionLog → ExecutionLogDto ───────────────────────
        // Outcome enum → string. TriggerUserName from entity's TriggerExternalUserName.
        CreateMap<AutomationExecutionLog, ExecutionLogDto>()
            .ForMember(d => d.TriggerUserName, o => o.MapFrom(s => s.TriggerExternalUserName ?? s.TriggerExternalUserId))
            .ForMember(d => d.Outcome, o => o.MapFrom(s => s.Outcome.ToString()));
    }
}
