// Infrastructure/Configurations/WorkspaceConfiguration.cs
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the Workspace entity.
/// Workspace is the Aggregate Root and tenant boundary — all data isolation in the system is based on WorkspaceId.
/// </summary>
public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        // Explicit table name for clarity and to prevent pluralization issues.
        builder.ToTable("Workspaces");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(w => w.Id);

        // ── Properties ──────────────────────────────────────────────────────

        // Organization or team name displayed in UI, invitations, and admin panels.
        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Unique URL-safe slug used for tenant routing (e.g., app.yourplatform.com/acme-corp).
        builder.Property(w => w.Slug)
            .IsRequired()
            .HasMaxLength(100);

        // Logo URL stored in Blob Storage — limited to standard URL length.
        builder.Property(w => w.LogoUrl)
            .HasMaxLength(2048);

        // Subscription plan stored as string for readability and debugging in raw DB queries.
        // TRD: "Plan influences limits for MaxSocialAccounts and MaxTeamMembers."
        builder.Property(w => w.Plan)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Subscription expiration — null means Free tier with no time limit.
        builder.Property(w => w.PlanExpiresAt);

        // Plan-level limits — stored as simple integers, enforced at Application layer.
        builder.Property(w => w.MaxSocialAccounts)
            .IsRequired();

        builder.Property(w => w.MaxTeamMembers)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // Soft deactivation flag — default true so new workspaces are active upon creation.
        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Slug must be globally unique — it serves as the URL identifier for tenant routing.
        builder.HasIndex(w => w.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Workspaces_Slug");

        // Performance index: filtering active workspaces in admin panels and scheduled jobs.
        builder.HasIndex(w => w.IsActive)
            .HasDatabaseName("IX_Workspaces_IsActive");

        // ── Relationships ───────────────────────────────────────────────────

        // One Workspace → Many WorkspaceMembers.
        // TRD: "All data belongs to Workspace, not directly to the user."
        builder.HasMany(w => w.Members)
            .WithOne(m => m.Workspace)
            .HasForeignKey(m => m.WorkspaceId)
            .HasConstraintName("FK_WorkspaceMembers_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many SocialAccounts.
        // TRD: "Connected external accounts belong to a Workspace."
        builder.HasMany(w => w.SocialAccounts)
            .WithOne(sa => sa.Workspace)
            .HasForeignKey(sa => sa.WorkspaceId)
            .HasConstraintName("FK_SocialAccounts_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many Posts.
        // TRD: "Posts are created within a Workspace context."
        builder.HasMany(w => w.Posts)
            .WithOne(p => p.Workspace)
            .HasForeignKey(p => p.WorkspaceId)
            .HasConstraintName("FK_Posts_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many AutomationRules.
        // TRD: "Automation rules operate within Workspace boundaries."
        builder.HasMany(w => w.AutomationRules)
            .WithOne(ar => ar.Workspace)
            .HasForeignKey(ar => ar.WorkspaceId)
            .HasConstraintName("FK_AutomationRules_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
