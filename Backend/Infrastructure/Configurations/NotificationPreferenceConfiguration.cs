// Infrastructure/Configurations/NotificationPreferenceConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the NotificationPreference entity.
/// Per-user, per-workspace, per-event notification settings.
/// TRD: "InAppEnabled — browser push; EmailEnabled — email; PushEnabled — mobile push."
/// </summary>
public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(np => np.Id);

        builder.Property(np => np.UserId).IsRequired();
        builder.Property(np => np.WorkspaceId).IsRequired();

        // Event type: PostPublished, PostFailed, NewInboxMessage, AutomationTriggered, etc.
        builder.Property(np => np.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Notification channels — all default to true so users get notified out of the box.
        builder.Property(np => np.InAppEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.EmailEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.PushEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique: one preference per user+workspace+event type — prevents duplicate settings.
        builder.HasIndex(np => new { np.UserId, np.WorkspaceId, np.EventType })
            .IsUnique()
            .HasDatabaseName("IX_NotificationPreferences_UserId_WorkspaceId_EventType");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Preferences → One User.
        // Cascade: deleting user removes all their notification settings.
        builder.HasOne(np => np.User)
            .WithMany(u => u.NotificationPreferences)
            .HasForeignKey(np => np.UserId)
            .HasConstraintName("FK_NotificationPreferences_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Many Preferences → One Workspace.
        // Cascade: deleting workspace removes related notification settings.
        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(np => np.WorkspaceId)
            .HasConstraintName("FK_NotificationPreferences_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
