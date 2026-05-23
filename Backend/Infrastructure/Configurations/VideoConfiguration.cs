// Infrastructure/Configurations/VideoConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the Video entity.
/// Video is an Aggregate Root representing an uploaded media file, independent of publications.
/// TRD: "One video can be used in multiple posts across platforms."
/// Implements soft delete via GlobalQueryFilter as per TRD architecture section.
/// </summary>
public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("Videos");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(v => v.Id);

        // ── Global Query Filter ─────────────────────────────────────────────
        // TRD: "Soft Delete for Video: physical deletion from Storage is a separate scheduled job."
        // GlobalQueryFilter ensures soft-deleted videos are excluded from all standard queries.
        builder.HasQueryFilter(v => v.DeletedAt == null);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(v => v.WorkspaceId)
            .IsRequired();

        // Who uploaded the file — for audit and "my uploads" filtering.
        builder.Property(v => v.UploadedByUserId)
            .IsRequired();

        // Blob Storage key — not a full URL; actual URL is generated dynamically via SAS/pre-signed URL.
        // TRD: "Example: workspaces/ws-abc123/videos/vid-def456.mp4"
        builder.Property(v => v.StorageKey)
            .IsRequired()
            .HasMaxLength(1000);

        // CDN URL for streaming and UI preview.
        builder.Property(v => v.CdnUrl)
            .HasMaxLength(2048);

        // Original filename from upload — helps users identify files in the media library.
        builder.Property(v => v.OriginalFileName)
            .IsRequired()
            .HasMaxLength(500);

        // MIME type: "video/mp4", "video/quicktime", etc.
        builder.Property(v => v.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        // File size in bytes — used for upload method selection (chunked for >50MB per TRD).
        builder.Property(v => v.FileSizeBytes)
            .IsRequired();

        // Auto-generated or manually uploaded thumbnail URL.
        builder.Property(v => v.ThumbnailUrl)
            .HasMaxLength(2048);

        // Video processing status stored as string.
        // TRD lifecycle: "Uploading → Processing → Ready → (Deleted)."
        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Error details when Status == Failed.
        builder.Property(v => v.ProcessingError)
            .HasMaxLength(4000);

        builder.Property(v => v.UploadedAt)
            .IsRequired();

        // Soft delete timestamp — populated when user "deletes" the video.
        // Physical deletion from Storage is handled by a separate Hangfire job.
        builder.Property(v => v.DeletedAt);

        // ── Owned Entities ──────────────────────────────────────────────────

        // VideoMetadata is a Value Object — stored in the same table as Video.
        // TRD: "Technical characteristics extracted after upload via FFprobe."
        builder.OwnsOne(v => v.Metadata, meta =>
        {
            // Duration in seconds — platform limits: TikTok ≤ 600s, Instagram Reels ≤ 90s, YouTube ≤ 12h.
            meta.Property(m => m.DurationSeconds)
                .HasColumnName("Metadata_DurationSeconds")
                .IsRequired();

            meta.Property(m => m.Width)
                .HasColumnName("Metadata_Width")
                .IsRequired();

            meta.Property(m => m.Height)
                .HasColumnName("Metadata_Height")
                .IsRequired();

            // Aspect ratio: "16:9", "9:16", "1:1", "4:5".
            meta.Property(m => m.AspectRatio)
                .HasColumnName("Metadata_AspectRatio")
                .IsRequired()
                .HasMaxLength(20);

            meta.Property(m => m.FrameRate)
                .HasColumnName("Metadata_FrameRate")
                .IsRequired();

            // Video codec: "h264", "h265", "vp9".
            meta.Property(m => m.VideoCodec)
                .HasColumnName("Metadata_VideoCodec")
                .IsRequired()
                .HasMaxLength(50);

            // Audio codec: "aac", "mp3", "opus" — null if no audio track.
            meta.Property(m => m.AudioCodec)
                .HasColumnName("Metadata_AudioCodec")
                .HasMaxLength(50);

            meta.Property(m => m.VideoBitrate)
                .HasColumnName("Metadata_VideoBitrate")
                .IsRequired();

            // Some platforms require audio — this flag enables validation.
            meta.Property(m => m.HasAudio)
                .HasColumnName("Metadata_HasAudio")
                .IsRequired();
        });

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique storage key — prevents duplicate uploads of the same file.
        builder.HasIndex(v => v.StorageKey)
            .IsUnique()
            .HasDatabaseName("IX_Videos_StorageKey");

        // Performance index for listing workspace videos.
        builder.HasIndex(v => v.WorkspaceId)
            .HasDatabaseName("IX_Videos_WorkspaceId");

        // Performance index for filtering by status (e.g., "all Processing videos" for monitoring).
        builder.HasIndex(v => v.Status)
            .HasDatabaseName("IX_Videos_Status");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Videos → One Workspace — configured from Workspace side.

        // Many Videos → One User (uploader).
        // Restrict: cannot delete a user who has uploaded videos without first handling the videos.
        builder.HasOne(v => v.UploadedBy)
            .WithMany()
            .HasForeignKey(v => v.UploadedByUserId)
            .HasConstraintName("FK_Videos_Users_UploadedByUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
