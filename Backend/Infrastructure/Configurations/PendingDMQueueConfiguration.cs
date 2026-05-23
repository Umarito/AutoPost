// Infrastructure/Configurations/PendingDMQueueConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the PendingDMQueue entity.
/// Key entity for handling private accounts — DMs are queued when direct sending is impossible.
/// TRD: "Hangfire job every 30 min checks Waiting entries; ExpiresAt default = TriggeredAt + 7 days."
/// </summary>
public class PendingDMQueueConfiguration : IEntityTypeConfiguration<PendingDMQueue>
{
    public void Configure(EntityTypeBuilder<PendingDMQueue> builder)
    {
        builder.ToTable("PendingDMQueue");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.AutomationRuleId).IsRequired();
        builder.Property(q => q.SocialAccountId).IsRequired();

        // External user ID to whom the DM needs to be sent.
        builder.Property(q => q.ExternalUserId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(q => q.ExternalUserName).HasMaxLength(500);

        // Final message text with variables already substituted at trigger time, not at send time.
        builder.Property(q => q.ResolvedMessageText)
            .IsRequired()
            .HasMaxLength(4000);

        // Reason why DM was not sent immediately: TargetAccountIsPrivate, ApiRateLimitReached, DMsDisabledByUser.
        builder.Property(q => q.Reason)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(q => q.TriggeredAt).IsRequired();
        builder.Property(q => q.LastCheckedAt);

        builder.Property(q => q.CheckAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Expiration — TRD: "Default: TriggeredAt + 7 days."
        builder.Property(q => q.ExpiresAt).IsRequired();

        // Queue status: Waiting, Sent, Expired, Cancelled.
        builder.Property(q => q.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Critical index for Hangfire polling job: finds all Waiting entries that haven't expired.
        // TRD: "Hangfire job every 30 min: scan Waiting entries."
        builder.HasIndex(q => new { q.Status, q.ExpiresAt })
            .HasDatabaseName("IX_PendingDMQueue_Status_ExpiresAt");

        builder.HasIndex(q => q.SocialAccountId)
            .HasDatabaseName("IX_PendingDMQueue_SocialAccountId");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Queue entries → One AutomationRule.
        // Restrict: cannot delete rule with pending DMs — must handle them first.
        builder.HasOne(q => q.AutomationRule)
            .WithMany()
            .HasForeignKey(q => q.AutomationRuleId)
            .HasConstraintName("FK_PendingDMQueue_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.Restrict);

        // Many Queue entries → One SocialAccount.
        // Restrict: cannot disconnect account with pending DMs.
        builder.HasOne(q => q.SocialAccount)
            .WithMany()
            .HasForeignKey(q => q.SocialAccountId)
            .HasConstraintName("FK_PendingDMQueue_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
