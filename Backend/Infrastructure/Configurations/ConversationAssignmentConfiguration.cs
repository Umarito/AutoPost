// Infrastructure/Configurations/ConversationAssignmentConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the ConversationAssignment entity.
/// One-to-one relationship with InboxConversation — each conversation has at most one active assignee.
/// TRD: "Distributes support workload among team members."
/// </summary>
public class ConversationAssignmentConfiguration : IEntityTypeConfiguration<ConversationAssignment>
{
    public void Configure(EntityTypeBuilder<ConversationAssignment> builder)
    {
        builder.ToTable("ConversationAssignments");
        builder.HasKey(a => a.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(a => a.ConversationId).IsRequired();
        builder.Property(a => a.AssignedToUserId).IsRequired();
        builder.Property(a => a.AssignedByUserId); // null = auto-assigned by AutomationRule
        builder.Property(a => a.AssignedAt).IsRequired();

        // Optional note when assigning: "Urgent, VIP client."
        builder.Property(a => a.Note).HasMaxLength(1000);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique: one conversation can have only one active assignment.
        builder.HasIndex(a => a.ConversationId)
            .IsUnique()
            .HasDatabaseName("IX_ConversationAssignments_ConversationId");

        // Performance: listing all conversations assigned to a specific team member.
        builder.HasIndex(a => a.AssignedToUserId)
            .HasDatabaseName("IX_ConversationAssignments_AssignedToUserId");

        // ── Relationships ───────────────────────────────────────────────────

        // One-to-One: Conversation ↔ Assignment.
        builder.HasOne(a => a.Conversation)
            .WithOne(c => c.Assignment)
            .HasForeignKey<ConversationAssignment>(a => a.ConversationId)
            .HasConstraintName("FK_ConversationAssignments_InboxConversations_ConversationId")
            .OnDelete(DeleteBehavior.Cascade);

        // Many Assignments → One User (assignee).
        // Restrict: cannot delete user who has active assignments.
        builder.HasOne(a => a.AssignedTo)
            .WithMany()
            .HasForeignKey(a => a.AssignedToUserId)
            .HasConstraintName("FK_ConversationAssignments_Users_AssignedToUserId")
            .OnDelete(DeleteBehavior.Restrict);

        // Many Assignments → One User (who assigned — optional).
        // Restrict: prevent cascade loops through multiple user FKs.
        builder.HasOne(a => a.AssignedBy)
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .HasConstraintName("FK_ConversationAssignments_Users_AssignedByUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
