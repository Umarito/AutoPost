// Infrastructure/Configurations/PostAnalyticsSnapshotConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="PostAnalyticsSnapshot"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures schema mappings for historical snapshot-based post analytics.</para>
/// <para><b>Business Justification:</b> Required to aggregate, plot, and track performance changes of a post on social networks over time.
/// TRD: "Analytics subsystem: store historical snapshots of post engagement metrics (Views, Likes, Comments, Shares, Saves, Reach, Impressions)."</para>
/// <para><b>Execution and Project Impact:</b> Essential for dashboard rendering and ROI calculations. Includes high-performance composite indexes to accelerate date-range filters.</para>
/// </remarks>
public class PostAnalyticsSnapshotConfiguration : IEntityTypeConfiguration<PostAnalyticsSnapshot>
{
    public void Configure(EntityTypeBuilder<PostAnalyticsSnapshot> builder)
    {
        // Table Name mapping
        builder.ToTable("PostAnalyticsSnapshots");

        // Primary Key definition
        builder.HasKey(s => s.Id);

        // Foreign Key to the Target Platform Post
        builder.Property(s => s.PostTargetId)
            .IsRequired();

        // Exact timestamp when the metrics snapshot was fetched
        builder.Property(s => s.RecordedAt)
            .IsRequired();

        // Standard engagement metrics
        builder.Property(s => s.Views)
            .IsRequired();

        builder.Property(s => s.Likes)
            .IsRequired();

        builder.Property(s => s.Comments)
            .IsRequired();

        builder.Property(s => s.Shares)
            .IsRequired();

        builder.Property(s => s.Saves)
            .IsRequired();

        // Advanced engagement metrics (optional, platform-specific)
        builder.Property(s => s.Reach);

        builder.Property(s => s.Impressions);

        // Video-specific performance indicators (optional)
        builder.Property(s => s.AverageWatchTime);

        builder.Property(s => s.CompletionRate);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite index for time-range analytics queries per TRD Analytics section.
        // Accelerates queries filtering by PostTargetId and ordering/filtering by RecordedAt.
        builder.HasIndex(s => new { s.PostTargetId, s.RecordedAt })
            .HasDatabaseName("IX_PostAnalyticsSnapshots_PostTargetId_RecordedAt");

        // ── Relationships ───────────────────────────────────────────────────

        // Relationship: One PostTarget -> Many AnalyticsSnapshots.
        // Cascade Delete: when a post target is deleted, all its history should be purged from DB.
        builder.HasOne(s => s.PostTarget)
            .WithMany()
            .HasForeignKey(s => s.PostTargetId)
            .HasConstraintName("FK_PostAnalyticsSnapshots_PostTargets_PostTargetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

