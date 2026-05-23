// Infrastructure/Configurations/InboxMessageConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the InboxMessage entity.
/// Represents a single message within a conversation — can be Inbound, Outbound, or Automated.
/// TRD: "IsAutomated flag and AutomationRuleId link distinguish bot-sent messages from manual ones."
/// </summary>
public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(m => m.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(m => m.ConversationId).IsRequired();

        // Platform-side message ID — used for reply, delete, and webhook deduplication.
        builder.Property(m => m.ExternalMessageId)
            .IsRequired()
            .HasMaxLength(500);

        // Direction: Inbound (from external user) or Outbound (from team).
        builder.Property(m => m.Direction)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Who sent the message from the team side — null for Inbound or Automated messages.
        builder.Property(m => m.SentByUserId);

        // Flag: message was sent by AutomationRule, not manually.
        builder.Property(m => m.IsAutomated)
            .IsRequired()
            .HasDefaultValue(false);

        // Link to the automation rule that generated this message.
        builder.Property(m => m.AutomationRuleId);

        // Content type: Text, Image, Video, Audio, Story, Sticker, Reaction, Unsupported.
        builder.Property(m => m.ContentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Text body of the message.
        builder.Property(m => m.TextContent).HasMaxLength(10000);

        // Media attachment URL (for non-text content types).
        builder.Property(m => m.MediaUrl).HasMaxLength(2048);

        // Timestamp from the platform (UTC).
        builder.Property(m => m.SentAt).IsRequired();

        // Whether any team member has read this message.
        builder.Property(m => m.IsReadByTeam)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.ReadAt);

        // Delivery status for outbound messages — null for inbound.
        builder.Property(m => m.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Performance: loading messages for a conversation chronologically.
        builder.HasIndex(m => new { m.ConversationId, m.SentAt })
            .HasDatabaseName("IX_InboxMessages_ConversationId_SentAt");

        // Deduplication: prevent processing the same webhook message twice.
        builder.HasIndex(m => m.ExternalMessageId)
            .HasDatabaseName("IX_InboxMessages_ExternalMessageId");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Messages → One Conversation — configured from InboxConversation side (Cascade).

        // Many Messages → One User (sender from team).
        // Restrict: cannot delete user who sent messages without handling them first.
        builder.HasOne(m => m.SentBy)
            .WithMany()
            .HasForeignKey(m => m.SentByUserId)
            .HasConstraintName("FK_InboxMessages_Users_SentByUserId")
            .OnDelete(DeleteBehavior.Restrict);

        // Many Messages → One AutomationRule (optional).
        // SetNull: if rule is deleted, message remains but loses the link.
        builder.HasOne(m => m.AutomationRule)
            .WithMany()
            .HasForeignKey(m => m.AutomationRuleId)
            .HasConstraintName("FK_InboxMessages_AutomationRules_AutomationRuleId")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
