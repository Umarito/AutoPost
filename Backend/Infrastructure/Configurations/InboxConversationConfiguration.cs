// Infrastructure/Configurations/InboxConversationConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the InboxConversation entity.
/// Aggregate Root for Unified Inbox — groups messages from one external user in one place.
/// TRD: "IsFollowingUs flag is critical for PendingDMQueue logic."
/// </summary>
public class InboxConversationConfiguration : IEntityTypeConfiguration<InboxConversation>
{
    public void Configure(EntityTypeBuilder<InboxConversation> builder)
    {
        builder.ToTable("InboxConversations");
        builder.HasKey(c => c.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(c => c.WorkspaceId).IsRequired();
        builder.Property(c => c.SocialAccountId).IsRequired();

        // Conversation type: DirectMessage, Comment, MentionReply.
        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Platform-side conversation ID — used for deduplication of webhook events.
        builder.Property(c => c.ExternalConversationId)
            .IsRequired()
            .HasMaxLength(500);

        // External user ID on the platform.
        builder.Property(c => c.ExternalUserId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.ExternalUserName).HasMaxLength(500);
        builder.Property(c => c.ExternalUserAvatarUrl).HasMaxLength(2048);

        // Critical for PendingDMQueue: if private account and not following → DM is deferred.
        builder.Property(c => c.IsFollowingUs)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.IsFollowingUsCheckedAt);

        // PostTarget reference — only for Comment-type conversations.
        builder.Property(c => c.PostTargetId);

        builder.Property(c => c.ExternalPostId).HasMaxLength(500);

        // Preview of the last message — displayed in conversation list UI.
        builder.Property(c => c.LastMessagePreview).HasMaxLength(500);
        builder.Property(c => c.LastMessageAt);

        // Inbox management status: Open, Resolved, Snoozed.
        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Unread count for badge counter in UI.
        builder.Property(c => c.UnreadCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.CreatedAt).IsRequired();

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique: one external conversation per social account — prevents webhook duplicates.
        builder.HasIndex(c => new { c.SocialAccountId, c.ExternalConversationId })
            .IsUnique()
            .HasDatabaseName("IX_InboxConversations_SocialAccountId_ExternalConversationId");

        // Performance: listing conversations in a workspace filtered by status.
        // TRD API: "GET /api/inbox/conversations — filters: platform, status, assignee."
        builder.HasIndex(c => new { c.WorkspaceId, c.Status })
            .HasDatabaseName("IX_InboxConversations_WorkspaceId_Status");

        // Performance: sorting by last message time.
        builder.HasIndex(c => c.LastMessageAt)
            .HasDatabaseName("IX_InboxConversations_LastMessageAt");

        // ── Relationships ───────────────────────────────────────────────────

        // Many Conversations → One SocialAccount.
        // Restrict: cannot disconnect account with active conversations.
        builder.HasOne(c => c.SocialAccount)
            .WithMany(sa => sa.Conversations)
            .HasForeignKey(c => c.SocialAccountId)
            .HasConstraintName("FK_InboxConversations_SocialAccounts_SocialAccountId")
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: conversation linked to a specific published post (Comment type only).
        // SetNull: if post target is deleted, conversation remains but loses the link.
        builder.HasOne(c => c.PostTarget)
            .WithMany()
            .HasForeignKey(c => c.PostTargetId)
            .HasConstraintName("FK_InboxConversations_PostTargets_PostTargetId")
            .OnDelete(DeleteBehavior.SetNull);

        // One Conversation → Many Messages.
        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .HasConstraintName("FK_InboxMessages_InboxConversations_ConversationId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
