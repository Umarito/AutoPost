using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// Configures workspace memberships and pending invitations.
/// </summary>
public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers");
        builder.HasKey(member => member.Id);

        builder.Property(member => member.WorkspaceId).IsRequired();
        builder.Property(member => member.UserId).IsRequired(false);
        builder.Property(member => member.Role).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(member => member.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(member => member.InvitedEmail).IsRequired().HasMaxLength(256);
        builder.Property(member => member.InvitedByUserId);
        builder.Property(member => member.InvitedAt).IsRequired();
        builder.Property(member => member.JoinedAt);

        builder.HasIndex(member => new { member.WorkspaceId, member.InvitedEmail })
            .IsUnique()
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId_InvitedEmail");

        builder.HasIndex(member => new { member.WorkspaceId, member.UserId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL")
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId_UserId");

        builder.HasIndex(member => member.WorkspaceId)
            .HasDatabaseName("IX_WorkspaceMembers_WorkspaceId");

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_WorkspaceMembers_UserId");

        builder.HasOne(member => member.User)
            .WithMany(user => user.WorkspaceMemberships)
            .HasForeignKey(member => member.UserId)
            .HasConstraintName("FK_WorkspaceMembers_Users_UserId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(member => member.InvitedBy)
            .WithMany()
            .HasForeignKey(member => member.InvitedByUserId)
            .HasConstraintName("FK_WorkspaceMembers_Users_InvitedByUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
