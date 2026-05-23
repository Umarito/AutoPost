using Application.DTOs.Auth;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Auth/User aggregate.
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>ApplicationUser → UserProfileDto</c> — Extracts safe profile fields, excluding Identity internals
/// (PasswordHash, SecurityStamp, ConcurrencyStamp, AccessFailedCount, LockoutEnd).</item>
/// </list>
/// </para>
///
/// <para><b>Security:</b>
/// This profile intentionally does NOT create a reverse map (UserProfileDto → ApplicationUser).
/// User creation is handled by ASP.NET Identity's UserManager, not AutoMapper.
/// Profile updates go through dedicated service methods that modify individual properties.</para>
///
/// <para><b>What is NOT mapped here:</b>
/// <c>RegisterRequest</c> and <c>LoginRequest</c> are consumed directly by the AuthService
/// and passed to Identity — no entity mapping needed. <c>AuthTokensDto</c> is constructed
/// manually by the service from JWT generation logic.</para>
/// </summary>
public class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        // ── ApplicationUser → UserProfileDto ───────────────────────────────
        // Maps only the safe, domain-level profile fields.
        // RegisteredAt on the entity maps to CreatedAt on the DTO for API consistency.
        CreateMap<ApplicationUser, UserProfileDto>()
            .ForMember(d => d.Email, o => o.MapFrom(s => s.Email!))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.RegisteredAt));
    }
}
