// Infrastructure/Configurations/PostAnalyticsSnapshotConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class PostAnalyticsSnapshotConfiguration : IEntityTypeConfiguration<PostAnalyticsSnapshot>
{
    public void Configure(EntityTypeBuilder<PostAnalyticsSnapshot> builder)
    {
        builder.ToTable("PostAnalyticsSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PostTargetId).IsRequired();
        builder.Property(s => s.RecordedAt).IsRequired();
        builder.Property(s => s.Views).IsRequired();
        builder.Property(s => s.Likes).IsRequired();
        builder.Property(s => s.Comments).IsRequired();
        builder.Property(s => s.Shares).IsRequired();
        builder.Property(s => s.Saves).IsRequired();
        builder.Property(s => s.Reach);
        builder.Property(s => s.Impressions);
        builder.Property(s => s.AverageWatchTime);
        builder.Property(s => s.CompletionRate);

        // Composite index for time-range analytics queries per TRD Analytics section.
        builder.HasIndex(s => new { s.PostTargetId, s.RecordedAt })
            .HasDatabaseName("IX_PostAnalyticsSnapshots_PostTargetId_RecordedAt");

        // Cascade: deleting a post target removes all its analytics.
        builder.HasOne(s => s.PostTarget)
            .WithMany()
            .HasForeignKey(s => s.PostTargetId)
            .HasConstraintName("FK_PostAnalyticsSnapshots_PostTargets_PostTargetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
