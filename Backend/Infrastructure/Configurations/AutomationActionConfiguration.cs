// Infrastructure/Configurations/AutomationActionConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the AutomationAction entity.
/// Actions execute in order of ExecutionOrder when a rule fires.
/// TRD: "1. LikeComment (instant) → 2. SendDM (45s delay — doesn't look like a bot) → 3. AddConversationTag."
/// </summary>
public class AutomationActionConfiguration : IEntityTypeConfiguration<AutomationAction>
{
    public void Configure(EntityTypeBuilder<AutomationAction> builder)
    {
        builder.ToTable("AutomationActions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AutomationRuleId).IsRequired();

        // Action type: SendDirectMessage, LikeComment, ReplyToComment, AddConversationTag, AssignToTeamMember.
        builder.Property(a => a.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Execution order for sequential actions (1, 2, 3...).
        builder.Property(a => a.ExecutionOrder)
            .IsRequired()
            .HasDefaultValue(1);

        // Delay in seconds before executing — 30-60s recommended for SendDM to mimic human behavior.
        builder.Property(a => a.DelaySeconds)
            .IsRequired()
            .HasDefaultValue(0);

        // Message template with variable support: {{username}}, {{post_url}}, {{comment_text}}, {{link}}.
        builder.Property(a => a.MessageTemplate).HasMaxLength(4000);

        // Specific link inserted as {{link}} in the template.
        builder.Property(a => a.LinkUrl).HasMaxLength(2048);

        // ── Indexes ─────────────────────────────────────────────────────────

        builder.HasIndex(a => a.AutomationRuleId)
            .HasDatabaseName("IX_AutomationActions_AutomationRuleId");

        // ── Relationships ───────────────────────────────────────────────────
        // Many Actions → One AutomationRule — configured from AutomationRule side (Cascade).
    }
}
