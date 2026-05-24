// Infrastructure/Configurations/WorkspaceConfiguration.cs
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Workspace"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures schema mappings and relationships for workspaces, acting as the multi-tenancy partition boundary.</para>
/// <para><b>Business Justification:</b> Workspace is the Aggregate Root and tenant boundary — all data isolation in the system is based on WorkspaceId.
/// TRD: "All data belongs to Workspace, not directly to the user. Max limits for accounts/members are enforced at the workspace level."</para>
/// <para><b>Execution and Project Impact:</b> Crucial for security and system architecture. Configures cascade behaviors.
/// <br/><i>Caution on Deletion:</i> Deleting a Workspace triggers cascade deletes on WorkspaceMembers, Posts, AutomationRules, and SocialAccounts.
/// However, if a SocialAccount has active conversations (InboxConversations), database-level <c>Restrict</c> rules on SocialAccount-to-Conversation relations
/// will block the delete operation, throwing a runtime exception. This is <b>intentional by design</b> (Not a Bug) to prevent accidental loss
/// of critical business conversational data without manual review/cleanup.</para>
/// </remarks>
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
        // Cascade: deleting a workspace removes all its memberships and invitations.
        builder.HasMany(w => w.Members)
            .WithOne(m => m.Workspace)
            .HasForeignKey(m => m.WorkspaceId)
            .HasConstraintName("FK_WorkspaceMembers_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many SocialAccounts.
        // Cascade: deleting a workspace removes connected platform accounts.
        // Note: Blocked at database level if any SocialAccount has related InboxConversations due to Restrict rules on SocialAccount.
        builder.HasMany(w => w.SocialAccounts)
            .WithOne(sa => sa.Workspace)
            .HasForeignKey(sa => sa.WorkspaceId)
            .HasConstraintName("FK_SocialAccounts_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many Posts.
        // Cascade: deleting a workspace purges all posts and media within it.
        builder.HasMany(w => w.Posts)
            .WithOne(p => p.Workspace)
            .HasForeignKey(p => p.WorkspaceId)
            .HasConstraintName("FK_Posts_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);

        // One Workspace → Many AutomationRules.
        // Cascade: deleting a workspace purges all automation rules and configurations.
        builder.HasMany(w => w.AutomationRules)
            .WithOne(ar => ar.Workspace)
            .HasForeignKey(ar => ar.WorkspaceId)
            .HasConstraintName("FK_AutomationRules_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

