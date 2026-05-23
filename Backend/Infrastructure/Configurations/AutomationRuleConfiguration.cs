// Infrastructure/Configurations/AutomationRuleConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the AutomationRule entity.
/// Aggregate Root for DM Automation — pattern: Trigger → Condition(s) → Action(s).
/// TRD: "Spam protection: MaxActionsPerUser + DailyExecutionLimit."
/// </summary>
public class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.HasKey(r => r.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(r => r.WorkspaceId).IsRequired();
        builder.Property(r => r.SocialAccountId).IsRequired();

        // Rule display name in UI.
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.Description).HasMaxLength(2000);

        // Enable/disable without deleting — TRD: "Disabled rule doesn't trigger but isn't deleted."
        builder.Property(r => r.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        // Trigger type: NewComment, NewFollower, StoryMention, DirectMessageReceived, CommentKeyword.
        builder.Property(r => r.TriggerType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Specific external post ID — null means rule applies to all posts.
        builder.Property(r => r.TargetExternalPostId).HasMaxLength(500);

        // Anti-spam: max triggers per unique external user. 0 = unlimited, 1 = recommended for SendDM.
        builder.Property(r => r.MaxActionsPerUser)
            .IsRequired()
            .HasDefaultValue(1);

        // Global daily limit — protection against mass spam.
        builder.Property(r => r.DailyExecutionLimit);

        // Today's counter — reset by a midnight Hangfire job.
        builder.Property(r => r.TodayExecutionCount)
            .IsRequired()
            .HasDefaultValue(0);

        // AuditableEntity timestamps.
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        // ── Indexes ─────────────────────────────────────────────────────────

        // Performance: listing enabled rules for a workspace.
        builder.HasIndex(r => new { r.WorkspaceId, r.IsEnabled })
            .HasDatabaseName("IX_AutomationRules_WorkspaceId_IsEnabled");

        // Performance: finding rules for a specific social account during webhook processing.
        builder.HasIndex(r => new { r.SocialAccountId, r.IsEnabled })
            .HasDatabaseName("IX_AutomationRules_SocialAccountId_IsEnabled");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Rules → One Workspace — configured from Workspace side (Cascade).

        // Many Rules → One SocialAccount.
        // Restrict: cannot disconnect account with active automation rules.
        builder.HasOne(r => r.SocialAccount)
            .WithMany(sa => sa.AutomationRules)
            .HasForeignKey(r => r.SocialAccountId)
            .HasConstraintName("FK_AutomationRules_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Restrict);

        // One Rule → Many TriggerConditions (AND logic).
        builder.HasMany(r => r.Conditions)
            .WithOne(tc => tc.AutomationRule)
            .HasForeignKey(tc => tc.AutomationRuleId)
            .HasConstraintName("FK_TriggerConditions_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Rule → Many AutomationActions (executed in order).
        builder.HasMany(r => r.Actions)
            .WithOne(a => a.AutomationRule)
            .HasForeignKey(a => a.AutomationRuleId)
            .HasConstraintName("FK_AutomationActions_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Rule → Many ExecutionLogs (audit trail).
        builder.HasMany(r => r.ExecutionLogs)
            .WithOne(el => el.AutomationRule)
            .HasForeignKey(el => el.AutomationRuleId)
            .HasConstraintName("FK_AutomationExecutionLogs_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
