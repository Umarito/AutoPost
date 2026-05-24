using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="WorkspaceMember"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures schema mappings for workspace memberships, user roles, and pending invitations.</para>
/// <para><b>Business Justification:</b> Manages RBAC (Role-Based Access Control) within workspaces. Handles the workflow from invitation to active membership.
/// TRD: "Workspace collaboration: workspaces have members (Owner, Admin, Member, Guest). Invitations are sent via email and accepted by users."</para>
/// <para><b>Execution and Project Impact:</b> Essential for security and multi-tenancy isolation. Includes unique partial indexes to guarantee data integrity (e.g., preventing duplicate memberships or pending invitations for the same email).</para>
/// </remarks>
public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        // Table Name mapping
        builder.ToTable("WorkspaceMembers");

        // Primary Key definition
        builder.HasKey(member => member.Id);

        // Foreign Key to the Workspace
        builder.Property(member => member.WorkspaceId)
            .IsRequired();

        // Foreign Key to the User (nullable until invitation is accepted)
        builder.Property(member => member.UserId)
            .IsRequired(false);

        // Membership role (Owner, Admin, Member, Guest)
        builder.Property(member => member.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Membership status (Invited, Active, Suspended, Declined)
        builder.Property(member => member.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Email address where the workspace invitation was sent
        builder.Property(member => member.InvitedEmail)
            .IsRequired()
            .HasMaxLength(256);

        // User who initiated the invitation
        builder.Property(member => member.InvitedByUserId);

        // Audit timestamps
        builder.Property(member => member.InvitedAt)
            .IsRequired();

        builder.Property(member => member.JoinedAt);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique index: prevents multiple invitations/memberships for the same email in a single workspace.
        builder.HasIndex(member => new { member.WorkspaceId, member.InvitedEmail })
            .IsUnique()
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId_InvitedEmail");

        // Unique index: prevents a single user from having multiple active membership records in a single workspace.
        // Uses a partial index filter since UserId is nullable for pending invitations.
        builder.HasIndex(member => new { member.WorkspaceId, member.UserId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL")
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId_UserId");

        // Foreign Key Index for WorkspaceId relationship queries
        builder.HasIndex(member => member.WorkspaceId)
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId");

        // Foreign Key Index for UserId relationship queries
        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_WorkspaceMembers_UserId");

        // ── Relationships ───────────────────────────────────────────────────

        // Relationship: One User -> Many WorkspaceMemberships.
        // Restrict Delete: users cannot be deleted if they own memberships; clean up memberships first.
        builder.HasOne(member => member.User)
            .WithMany(user => user.WorkspaceMemberships)
            .HasForeignKey(member => member.UserId)
            .HasConstraintName("FK_WorkspaceMembers_Users_UserId")
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship: One User (Inviter) -> Many WorkspaceInvitations.
        // Restrict Delete: prevent deleting a user who invited others without handling audit logs.
        builder.HasOne(member => member.InvitedBy)
            .WithMany()
            .HasForeignKey(member => member.InvitedByUserId)
            .HasConstraintName("FK_WorkspaceMembers_Users_InvitedByUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

