// Infrastructure/Configurations/PostConfiguration.cs
using System.Text.Json;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the Post entity.
/// Post is the core Aggregate Root — it represents the intent to publish content to one or more platforms.
/// TRD lifecycle: "Draft → Scheduled → Publishing → Published / PartiallyFailed / Failed / Cancelled."
/// </summary>
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("Posts");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(p => p.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(p => p.WorkspaceId)
            .IsRequired();

        // Author of the post — for audit trail and "my posts" filtering.
        builder.Property(p => p.CreatedByUserId)
            .IsRequired();

        // Optional video reference — null reserved for future text-only posts.
        builder.Property(p => p.VideoId);

        // Post lifecycle status stored as string.
        // TRD: "Draft → Scheduled → Publishing → Published / PartiallyFailed / Failed / Cancelled."
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Auditable timestamps — inherited from AuditableEntity<Guid>.
        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Actual publication completion time — may differ from ScheduledAt due to Hangfire delays or retries.
        builder.Property(p => p.CompletedAt);

        // ── Owned Entities ──────────────────────────────────────────────────

        // PostContent Value Object — all textual content stored in the same table.
        builder.OwnsOne(p => p.Content, content =>
        {
            // Title — required for YouTube, optional for Instagram/TikTok.
            content.Property(c => c.Title)
                .HasColumnName("Content_Title")
                .HasMaxLength(500);

            // Description / caption — main text body.
            content.Property(c => c.Description)
                .HasColumnName("Content_Description")
                .HasMaxLength(10000);

            // Tags stored as JSON column — TRD: "Hashtags without # symbol: ['marketing', 'tutorial']."
            var tagsConverter = new ValueConverter<IReadOnlyList<string>, string>(
                tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null)
                        ?? new List<string>());

            var tagsComparer = new ValueComparer<IReadOnlyList<string>>(
                (left, right) => left == right || (left != null && right != null && left.SequenceEqual(right)),
                tags => tags == null
                    ? 0
                    : tags.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
                tags => tags == null ? Array.Empty<string>() : (IReadOnlyList<string>)tags.ToList());

            content.Property(c => c.Tags)
                .HasColumnName("Content_Tags")
                .HasConversion(tagsConverter)
                .Metadata.SetValueComparer(tagsComparer);

            // Visibility: Public, Unlisted (link-only), Private.
            content.Property(c => c.Visibility)
                .HasColumnName("Content_Visibility")
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            // Custom thumbnail URL — overrides auto-generated Video.ThumbnailUrl if set.
            content.Property(c => c.CustomThumbnailUrl)
                .HasColumnName("Content_CustomThumbnailUrl")
                .HasMaxLength(2048);

            // Platform-specific settings as raw JSON — varies per platform.
            // TRD: "YouTube: { madeForKids, categoryId, license }, TikTok: { allowDuet, allowStitch }."
            content.Property(c => c.PlatformSettingsJson)
                .HasColumnName("Content_PlatformSettingsJson")
                .HasMaxLength(8000);
        });

        // Schedule Value Object — publication timing.
        builder.OwnsOne(p => p.Schedule, schedule =>
        {
            // Publication time always in UTC — no exceptions.
            // TRD: "All ScheduledAt stored and accepted in UTC, conversion only on frontend."
            schedule.Property(s => s.ScheduledAt)
                .HasColumnName("Schedule_ScheduledAt")
                .IsRequired();

            // IANA timezone string for display purposes.
            schedule.Property(s => s.TimeZoneId)
                .HasColumnName("Schedule_TimeZoneId")
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("UTC");

            // Hangfire job ID — used for cancellation or rescheduling.
            schedule.Property(s => s.SchedulerJobId)
                .HasColumnName("Schedule_SchedulerJobId")
                .HasMaxLength(200);
        });

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite index for the most common query: "list posts in workspace filtered by status."
        // TRD API: "GET /api/posts — filters: status, platform, date range."
        builder.HasIndex(p => new { p.WorkspaceId, p.Status })
            .HasDatabaseName("IX_Posts_WorkspaceId_Status");

        // Performance index for filtering posts by creator.
        builder.HasIndex(p => p.CreatedByUserId)
            .HasDatabaseName("IX_Posts_CreatedByUserId");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Posts → One Workspace — configured from Workspace side (Cascade).

        // Many Posts → One User (creator).
        // Restrict: cannot delete a user who created posts without first handling the posts.
        builder.HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .HasConstraintName("FK_Posts_Users_CreatedByUserId")
            .OnDelete(DeleteBehavior.Restrict);

        // Many Posts → One Video (optional).
        // SetNull: if a video is deleted, the post remains but loses its video reference.
        // TRD: "One video can be used in multiple posts."
        builder.HasOne(p => p.Video)
            .WithMany(v => v.Posts)
            .HasForeignKey(p => p.VideoId)
            .HasConstraintName("FK_Posts_Videos_VideoId")
            .OnDelete(DeleteBehavior.SetNull);

        // One Post → Many PostTargets.
        // TRD: "Post is the aggregate root and owns PostTargets."
        builder.HasMany(p => p.Targets)
            .WithOne(pt => pt.Post)
            .HasForeignKey(pt => pt.PostId)
            .HasConstraintName("FK_PostTargets_Posts_PostId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
