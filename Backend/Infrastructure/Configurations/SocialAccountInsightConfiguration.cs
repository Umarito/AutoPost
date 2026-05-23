// Infrastructure/Configurations/SocialAccountInsightConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the SocialAccountInsight entity.
/// SocialAccountInsight stores time-series analytics snapshots (followers, reach, impressions).
/// TRD: "One record per day/week — used for growth charts in the dashboard."
/// </summary>
public class SocialAccountInsightConfiguration : IEntityTypeConfiguration<SocialAccountInsight>
{
    public void Configure(EntityTypeBuilder<SocialAccountInsight> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("SocialAccountInsights");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(i => i.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(i => i.SocialAccountId)
            .IsRequired();

        // UTC timestamp of the snapshot — typically once daily.
        builder.Property(i => i.RecordedAt)
            .IsRequired();

        builder.Property(i => i.FollowersCount)
            .IsRequired();

        builder.Property(i => i.FollowingCount)
            .IsRequired();

        builder.Property(i => i.TotalPostsCount)
            .IsRequired();

        // Nullable metrics — not all platforms provide reach and impressions.
        builder.Property(i => i.ProfileReach);

        builder.Property(i => i.ProfileImpressions);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite index for time-range queries: "Get insights for account X from date A to B."
        // Essential for dashboard growth charts per TRD Analytics section.
        builder.HasIndex(i => new { i.SocialAccountId, i.RecordedAt })
            .HasDatabaseName("IX_SocialAccountInsights_AccountId_RecordedAt");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Insights → One SocialAccount.
        // Cascade: deleting a social account removes all its analytics history.
        builder.HasOne(i => i.SocialAccount)
            .WithMany()
            .HasForeignKey(i => i.SocialAccountId)
            .HasConstraintName("FK_SocialAccountInsights_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
