// Infrastructure/Configurations/PendingDMQueueConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="PendingDMQueue"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures the relational mappings for the Direct Message queue.</para>
/// <para><b>Business Justification:</b> Key entity for handling private accounts — DMs are queued when direct sending is impossible.
/// TRD: "Hangfire job every 30 min checks Waiting entries; ExpiresAt default = TriggeredAt + 7 days."</para>
/// <para><b>Execution and Project Impact:</b> Essential for messaging reliability. Includes crucial indexes for the background polling job to avoid database table scans.</para>
/// </remarks>
public class PendingDMQueueConfiguration : IEntityTypeConfiguration<PendingDMQueue>
{
    public void Configure(EntityTypeBuilder<PendingDMQueue> builder)
    {
        // Table Name mapping
        builder.ToTable("PendingDMQueue");

        // Primary Key definition
        builder.HasKey(q => q.Id);

        // Foreign Key IDs
        builder.Property(q => q.AutomationRuleId).IsRequired();
        builder.Property(q => q.SocialAccountId).IsRequired();

        // External platform user identifier
        builder.Property(q => q.ExternalUserId)
            .IsRequired()
            .HasMaxLength(500);

        // Cached username of the recipient
        builder.Property(q => q.ExternalUserName).HasMaxLength(500);

        // Pre-resolved message content with tokens substituted
        builder.Property(q => q.ResolvedMessageText)
            .IsRequired()
            .HasMaxLength(4000);

        // Failure reason categorization
        builder.Property(q => q.Reason)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Temporal tracking
        builder.Property(q => q.TriggeredAt).IsRequired();
        builder.Property(q => q.LastCheckedAt);

        // Retry and rate-limiting limits
        builder.Property(q => q.CheckAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Expiration threshold
        builder.Property(q => q.ExpiresAt).IsRequired();

        // Queue state machine representation
        builder.Property(q => q.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite index for background Hangfire polling job: filters for active "Waiting" messages.
        // TRD: "Hangfire job every 30 min: scan Waiting entries."
        builder.HasIndex(q => new { q.Status, q.ExpiresAt })
            .HasDatabaseName("IX_PendingDMQueue_Status_ExpiresAt");

        // Foreign Key Index for SocialAccountId relationship queries
        builder.HasIndex(q => q.SocialAccountId)
            .HasDatabaseName("IX_PendingDMQueue_SocialAccountId");

        // Foreign Key Index for AutomationRuleId relationship queries (Search & Destroy: prevent full table scan on rule updates)
        builder.HasIndex(q => q.AutomationRuleId)
            .HasDatabaseName("IX_PendingDMQueue_AutomationRuleId");

        // ── Relationships ───────────────────────────────────────────────────

        // AutomationRule Relationship: Restrict deletion of rules linked to pending messages
        builder.HasOne(q => q.AutomationRule)
            .WithMany()
            .HasForeignKey(q => q.AutomationRuleId)
            .HasConstraintName("FK_PendingDMQueue_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.Restrict);

        // SocialAccount Relationship: Restrict disconnection of accounts with pending messages
        builder.HasOne(q => q.SocialAccount)
            .WithMany()
            .HasForeignKey(q => q.SocialAccountId)
            .HasConstraintName("FK_PendingDMQueue_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

