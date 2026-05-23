// Infrastructure/Configurations/PostTargetConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the PostTarget entity.
/// PostTarget represents a specific publishing destination — one Post can target multiple platforms.
/// TRD: "One Post has multiple PostTargets, each tracked independently."
/// </summary>
public class PostTargetConfiguration : IEntityTypeConfiguration<PostTarget>
{
    public void Configure(EntityTypeBuilder<PostTarget> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("PostTargets");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(pt => pt.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(pt => pt.PostId)
            .IsRequired();

        builder.Property(pt => pt.SocialAccountId)
            .IsRequired();

        // Denormalized platform field for fast queries without JOIN.
        // TRD: "Allows filtering 'all Instagram posts' without loading SocialAccount."
        builder.Property(pt => pt.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Individual target status stored as string.
        // TRD: "Pending → Publishing → Published / Failed / Retrying."
        builder.Property(pt => pt.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // ── Owned Entities ──────────────────────────────────────────────────

        // PostTargetResult Value Object — populated after publish attempt completes.
        builder.OwnsOne(pt => pt.Result, result =>
        {
            // External post ID from the platform — used for analytics API calls.
            result.Property(r => r.ExternalPostId)
                .HasColumnName("Result_ExternalPostId")
                .HasMaxLength(500);

            // Direct link to the published post.
            result.Property(r => r.ExternalPostUrl)
                .HasColumnName("Result_ExternalPostUrl")
                .HasMaxLength(2048);

            // Actual publication time as reported by the platform.
            result.Property(r => r.PublishedAt)
                .HasColumnName("Result_PublishedAt");

            // Platform API error code on failure.
            result.Property(r => r.ErrorCode)
                .HasColumnName("Result_ErrorCode")
                .HasMaxLength(200);

            // Platform API error message on failure.
            result.Property(r => r.ErrorMessage)
                .HasColumnName("Result_ErrorMessage")
                .HasMaxLength(4000);

            // Number of publish attempts including retries.
            result.Property(r => r.AttemptCount)
                .HasColumnName("Result_AttemptCount");
        });

        // ── Indexes ─────────────────────────────────────────────────────────

        // Performance index for loading all targets for a post.
        builder.HasIndex(pt => pt.PostId)
            .HasDatabaseName("IX_PostTargets_PostId");

        // Performance index for filtering targets by platform.
        builder.HasIndex(pt => pt.Platform)
            .HasDatabaseName("IX_PostTargets_Platform");

        // Composite index for querying target status per social account.
        builder.HasIndex(pt => new { pt.SocialAccountId, pt.Status })
            .HasDatabaseName("IX_PostTargets_SocialAccountId_Status");

        // ── Relationships ───────────────────────────────────────────────────

        // Many PostTargets → One Post — configured from Post side (Cascade).

        // Many PostTargets → One SocialAccount.
        // Restrict: cannot disconnect a social account that has active publish targets.
        builder.HasOne(pt => pt.SocialAccount)
            .WithMany(sa => sa.PostTargets)
            .HasForeignKey(pt => pt.SocialAccountId)
            .HasConstraintName("FK_PostTargets_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Restrict);

        // One PostTarget → Many PublishingJobs — debugging history.
        builder.HasMany(pt => pt.PublishingJobs)
            .WithOne(pj => pj.PostTarget)
            .HasForeignKey(pj => pj.PostTargetId)
            .HasConstraintName("FK_PublishingJobs_PostTargets_PostTargetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
