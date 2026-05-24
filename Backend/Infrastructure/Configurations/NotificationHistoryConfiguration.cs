using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="NotificationHistory"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures database mappings for persisted operational notification histories.</para>
/// <para><b>Business Justification:</b> Required to back the in-app notification center and support multi-channel delivery audits.
/// TRD: "System Notifications: send workspace alerts (email/in-app) on automation rule triggers, post publishes, and errors."</para>
/// <para><b>Execution and Project Impact:</b> Crucial for user alerting. Utilizes composite indexes optimized for retrieval.
/// Note: A solo index on UserId is redundant because UserId is the leading column of the composite index
/// IX_NotificationHistories_UserId_WorkspaceId_CreatedAt, which fully covers queries filtered by UserId.</para>
/// </remarks>
public sealed class NotificationHistoryConfiguration : IEntityTypeConfiguration<NotificationHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationHistory> builder)
    {
        // Table Name mapping
        builder.ToTable("NotificationHistories");

        // Primary Key definition
        builder.HasKey(history => history.Id);

        // Foreign Key to the User
        builder.Property(history => history.UserId)
            .IsRequired();

        // Foreign Key to the Workspace context
        builder.Property(history => history.WorkspaceId)
            .IsRequired();

        // System notification event categorization
        builder.Property(history => history.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);

        // Delivery channel type (InApp, Email, Push)
        builder.Property(history => history.Channel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // User-facing text properties
        builder.Property(history => history.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(history => history.Body)
            .IsRequired()
            .HasMaxLength(4000);

        // Optional deep-linking redirection target
        builder.Property(history => history.ActionUrl)
            .HasMaxLength(2048);

        // Temporal tracking
        builder.Property(history => history.CreatedAt)
            .IsRequired();

        builder.Property(history => history.DeliveredAt);

        // Operational audit state
        builder.Property(history => history.IsDelivered)
            .IsRequired()
            .HasDefaultValue(false);

        // Delivery failure logging
        builder.Property(history => history.DeliveryError)
            .HasMaxLength(2000);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite Index for User In-App Notification Center: filters by User, Workspace, and sorts chronologically.
        // UserId is the leading column of this B-Tree index, which means it fully covers queries like WHERE UserId = X,
        // rendering an additional solo index on UserId completely redundant and saving storage/write performance.
        builder.HasIndex(history => new { history.UserId, history.WorkspaceId, history.CreatedAt })
            .HasDatabaseName("IX_NotificationHistories_UserId_WorkspaceId_CreatedAt");

        // Composite Index for Workspace Operational Analytics: groups by Workspace, EventType, and Channel.
        builder.HasIndex(history => new { history.WorkspaceId, history.EventType, history.Channel })
            .HasDatabaseName("IX_NotificationHistories_WorkspaceId_EventType_Channel");

        // ── Relationships ───────────────────────────────────────────────────

        // Relationship: One User -> Many NotificationHistories.
        // Cascade Delete: when a user account is deleted, all their notification history is purged.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.UserId)
            .HasConstraintName("FK_NotificationHistories_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship: One Workspace -> Many NotificationHistories.
        // Cascade Delete: when a workspace is deleted, all its linked notification logs are purged.
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(history => history.WorkspaceId)
            .HasConstraintName("FK_NotificationHistories_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

