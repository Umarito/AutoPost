using Application.DTOs.Video;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for the Video aggregate.
///
/// <para><b>Mappings handled:</b>
/// <list type="bullet">
/// <item><c>Video → VideoSummaryDto</c> — Extracts DurationSeconds from the nested Metadata
/// navigation. Status enum → string.</item>
/// <item><c>Video → VideoDetailDto</c> — Full video with nested VideoMetadataDto and the
/// uploader's display name from the User navigation.</item>
/// <item><c>VideoMetadata → VideoMetadataDto</c> — Direct 1:1 property mapping (all names match).</item>
/// </list>
/// </para>
///
/// <para><b>Nullable Metadata handling:</b>
/// If a video hasn't been processed yet by FFprobe, its Metadata property is null.
/// For VideoSummaryDto, DurationSeconds maps to null. For VideoDetailDto, the entire
/// Metadata sub-object maps to null. AutoMapper handles this gracefully by default.</para>
/// </summary>
public class VideoMappingProfile : Profile
{
    public VideoMappingProfile()
    {
        // ── Video → VideoSummaryDto ────────────────────────────────────────
        // DurationSeconds comes from the nested Metadata value object (nullable).
        // Status enum → string for API serialization.
        CreateMap<Video, VideoSummaryDto>()
            .ForMember(d => d.DurationSeconds, o => o.MapFrom(s =>
                s.Metadata != null ? s.Metadata.DurationSeconds : (int?)null))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ── Video → VideoDetailDto ─────────────────────────────────────────
        // Adds CdnUrl, full Metadata sub-object, and uploader's DisplayName.
        // UploadedBy navigation must be .Include()-d in the repository query.
        CreateMap<Video, VideoDetailDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.UploadedByDisplayName, o => o.MapFrom(s => s.UploadedBy.DisplayName));

        // ── VideoMetadata → VideoMetadataDto ───────────────────────────────
        // 1:1 mapping — all property names and types match exactly.
        CreateMap<VideoMetadata, VideoMetadataDto>();
    }
}
