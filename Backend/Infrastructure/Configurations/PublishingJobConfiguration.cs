// Infrastructure/Configurations/PublishingJobConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the PublishingJob entity.
/// PublishingJob logs each individual publishing attempt for debugging.
/// TRD: "Invaluable for debugging: 'Why didn't the post go to TikTok at 14:00? See PublishingJob #3 → RawApiResponse.'"
/// </summary>
public class PublishingJobConfiguration : IEntityTypeConfiguration<PublishingJob>
{
    public void Configure(EntityTypeBuilder<PublishingJob> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("PublishingJobs");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(pj => pj.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(pj => pj.PostTargetId)
            .IsRequired();

        // Hangfire job ID — for correlation with scheduler dashboard logs.
        builder.Property(pj => pj.SchedulerJobId)
            .HasMaxLength(200);

        // Attempt number: 1 = first try, 2/3 = retries after errors.
        builder.Property(pj => pj.AttemptNumber)
            .IsRequired();

        builder.Property(pj => pj.StartedAt)
            .IsRequired();

        builder.Property(pj => pj.CompletedAt);

        // Job outcome stored as string.
        // TRD: "InProgress, Succeeded, Failed, Retrying."
        builder.Property(pj => pj.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Full stack trace on error — internal audit only, never exposed in API responses.
        builder.Property(pj => pj.ErrorDetails)
            .HasMaxLength(8000);

        // Raw JSON response from platform API — saved for both success and failure.
        // TRD: "Critically useful for debugging non-obvious errors."
        builder.Property(pj => pj.RawApiResponse)
            .HasMaxLength(16000);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Performance index for loading all jobs for a target.
        builder.HasIndex(pj => pj.PostTargetId)
            .HasDatabaseName("IX_PublishingJobs_PostTargetId");

        // ── Relationships ───────────────────────────────────────────────────
        // Many PublishingJobs → One PostTarget — configured from PostTarget side (Cascade).
    }
}
