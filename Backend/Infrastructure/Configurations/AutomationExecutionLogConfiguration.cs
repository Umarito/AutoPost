// Infrastructure/Configurations/AutomationExecutionLogConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the AutomationExecutionLog entity.
/// Detailed log of every rule execution — used for debugging, idempotency, and analytics.
/// TRD: "Idempotency: duplicate ExternalTriggerEventId → return Skipped, don't re-execute."
/// </summary>
public class AutomationExecutionLogConfiguration : IEntityTypeConfiguration<AutomationExecutionLog>
{
    public void Configure(EntityTypeBuilder<AutomationExecutionLog> builder)
    {
        builder.ToTable("AutomationExecutionLogs");
        builder.HasKey(el => el.Id);

        builder.Property(el => el.AutomationRuleId).IsRequired();

        // Platform event ID — idempotency key. For comments: comment ID, for follows: follower ID.
        builder.Property(el => el.ExternalTriggerEventId)
            .IsRequired()
            .HasMaxLength(500);

        // External user whose action triggered the rule.
        builder.Property(el => el.TriggerExternalUserId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(el => el.TriggerExternalUserName).HasMaxLength(500);

        // Original trigger content (comment text, message text, etc.).
        builder.Property(el => el.TriggerContent).HasMaxLength(4000);

        builder.Property(el => el.ExecutedAt).IsRequired();

        // Outcome: Executed, Skipped, Pending, Failed.
        builder.Property(el => el.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Skip reason when Outcome == Skipped.
        builder.Property(el => el.SkipReason).HasMaxLength(500);

        // API error text when Outcome == Failed.
        builder.Property(el => el.ErrorMessage).HasMaxLength(4000);

        // Link to PendingDMQueue when Outcome == Pending.
        builder.Property(el => el.PendingDMQueueId);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique: idempotency — prevents re-processing the same platform event.
        // TRD: "ExternalTriggerEventId used as idempotency key."
        builder.HasIndex(el => new { el.AutomationRuleId, el.ExternalTriggerEventId })
            .IsUnique()
            .HasDatabaseName("IX_AutomationExecutionLogs_RuleId_ExternalEventId");

        // Performance: listing logs for a specific rule (paginated).
        // TRD API: "GET /api/automation/rules/{id}/logs"
        builder.HasIndex(el => new { el.AutomationRuleId, el.ExecutedAt })
            .HasDatabaseName("IX_AutomationExecutionLogs_RuleId_ExecutedAt");

        // ── Relationships ───────────────────────────────────────────────────
        // Many Logs → One AutomationRule — configured from AutomationRule side (Cascade).
    }
}
