using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// Конфигурирует persisted operational-историю уведомлений.
/// История хранится отдельно от пользовательских настроек, потому что TRD требует notification center и аудит доставки.
/// </summary>
public sealed class NotificationHistoryConfiguration : IEntityTypeConfiguration<NotificationHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationHistory> builder)
    {
        builder.ToTable("NotificationHistories");
        builder.HasKey(history => history.Id);

        builder.Property(history => history.UserId).IsRequired();
        builder.Property(history => history.WorkspaceId).IsRequired();

        builder.Property(history => history.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);

        builder.Property(history => history.Channel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(history => history.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(history => history.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(history => history.ActionUrl)
            .HasMaxLength(2048);

        builder.Property(history => history.CreatedAt).IsRequired();
        builder.Property(history => history.DeliveredAt);
        builder.Property(history => history.IsDelivered).IsRequired().HasDefaultValue(false);
        builder.Property(history => history.DeliveryError).HasMaxLength(2000);

        builder.HasIndex(history => new { history.UserId, history.WorkspaceId, history.CreatedAt })
            .HasDatabaseName("IX_NotificationHistories_UserId_WorkspaceId_CreatedAt");

        builder.HasIndex(history => new { history.WorkspaceId, history.EventType, history.Channel })
            .HasDatabaseName("IX_NotificationHistories_WorkspaceId_EventType_Channel");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.UserId)
            .HasConstraintName("FK_NotificationHistories_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(history => history.WorkspaceId)
            .HasConstraintName("FK_NotificationHistories_Workspaces_WorkspaceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
