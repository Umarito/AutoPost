using Application.DTOs.SocialAccount;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the SocialAccount aggregate.
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>SocialAccount → SocialAccountDto</c> — Converts Platform and Status enums to strings,
/// splits the GrantedScopes CSV string into a string array for the API.</item>
/// </list>
/// </para>
///
/// <para><b>Security — Token exclusion:</b>
/// The entity contains EncryptedAccessToken, EncryptedRefreshToken, and TokenExpiresAt.
/// These fields are intentionally NOT mapped to the DTO. The SocialAccountDto record
/// does not include them, so AutoMapper automatically ignores them (no matching target property).
/// This is the primary security boundary for OAuth credentials.</para>
///
/// <para><b>GrantedScopes transformation:</b>
/// The entity stores scopes as a comma-separated string (e.g., "publish,read_insights").
/// The DTO exposes them as <c>string[]</c> for the API. The .Split(',') call handles this
/// conversion. Empty strings are filtered out to prevent trailing-comma edge cases.</para>
/// </summary>
public class SocialAccountMappingProfile : Profile
{
    public SocialAccountMappingProfile()
    {
        // ── SocialAccount → SocialAccountDto ───────────────────────────────
        // Platform/Status → string. GrantedScopes CSV → string[].
        // Sensitive fields (tokens) are excluded by DTO design — no target properties exist.
        CreateMap<SocialAccount, SocialAccountDto>()
            .ForMember(d => d.Platform, o => o.MapFrom(s => s.Platform.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.GrantedScopes, o => o.MapFrom(s =>
                string.IsNullOrEmpty(s.GrantedScopes)
                    ? Array.Empty<string>()
                    : s.GrantedScopes.Split(',', StringSplitOptions.RemoveEmptyEntries)));
    }
}
