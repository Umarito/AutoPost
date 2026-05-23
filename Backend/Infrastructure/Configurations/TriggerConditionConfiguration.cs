// Infrastructure/Configurations/TriggerConditionConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the TriggerCondition entity.
/// Multiple conditions in one rule use AND logic — all must match for the rule to fire.
/// TRD: "CommentText Contains 'price' AND CommentText Contains 'discount' → both must be present."
/// </summary>
public class TriggerConditionConfiguration : IEntityTypeConfiguration<TriggerCondition>
{
    public void Configure(EntityTypeBuilder<TriggerCondition> builder)
    {
        builder.ToTable("TriggerConditions");
        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.AutomationRuleId).IsRequired();

        // Condition type: CommentText, CommentAuthorIsFollower, AccountIsPublic, FirstTimeCommenter.
        builder.Property(tc => tc.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Operator: Contains, Equals, StartsWith, EndsWith, Any, NotContains.
        builder.Property(tc => tc.Operator)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Comparison value — null when Operator == Any (any value passes).
        builder.Property(tc => tc.Value).HasMaxLength(1000);

        // Case sensitivity — recommended false for broader matching.
        builder.Property(tc => tc.IsCaseSensitive)
            .IsRequired()
            .HasDefaultValue(false);

        // ── Indexes ─────────────────────────────────────────────────────────

        builder.HasIndex(tc => tc.AutomationRuleId)
            .HasDatabaseName("IX_TriggerConditions_AutomationRuleId");

        // ── Relationships ───────────────────────────────────────────────────
        // Many Conditions → One AutomationRule — configured from AutomationRule side (Cascade).
    }
}
