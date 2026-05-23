// Infrastructure/Configurations/SocialAccountConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the SocialAccount entity.
/// SocialAccount is the bridge between the system and external platforms (YouTube, Instagram, TikTok, etc.).
/// TRD: "OAuth tokens are stored ENCRYPTED via IDataProtectionProvider. Plaintext NEVER reaches DB, logs, or API responses."
/// </summary>
public class SocialAccountConfiguration : IEntityTypeConfiguration<SocialAccount>
{
    public void Configure(EntityTypeBuilder<SocialAccount> builder)
    {
        // ── Table ────────────────────────────────────────────────────────────
        builder.ToTable("SocialAccounts");

        // ── Primary Key ─────────────────────────────────────────────────────
        builder.HasKey(sa => sa.Id);

        // ── Properties ──────────────────────────────────────────────────────

        builder.Property(sa => sa.WorkspaceId)
            .IsRequired();

        // Platform enum stored as string for readability.
        // TRD: "YouTube, Instagram, Facebook, TikTok, Twitter, Telegram."
        builder.Property(sa => sa.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Unique ID on the platform side (e.g., "UC_x5XG1OV2P6uZZ5FSM9Ttw" for YouTube).
        builder.Property(sa => sa.ExternalAccountId)
            .IsRequired()
            .HasMaxLength(500);

        // Display name of the channel/page as shown on the platform.
        builder.Property(sa => sa.AccountDisplayName)
            .IsRequired()
            .HasMaxLength(500);

        // Optional @handle or username on the platform.
        builder.Property(sa => sa.AccountUsername)
            .HasMaxLength(500);

        // Cached avatar URL to avoid API calls on every UI render.
        builder.Property(sa => sa.AccountAvatarUrl)
            .HasMaxLength(2048);

        // Encrypted OAuth Access Token — NEVER stored as plaintext.
        // TRD Security: "Tokens encrypted via IDataProtectionProvider."
        builder.Property(sa => sa.EncryptedAccessToken)
            .IsRequired()
            .HasMaxLength(4000);

        // Encrypted OAuth Refresh Token — null for platforms without refresh support.
        builder.Property(sa => sa.EncryptedRefreshToken)
            .HasMaxLength(4000);

        // Access token expiration in UTC.
        builder.Property(sa => sa.TokenExpiresAt)
            .IsRequired();

        // OAuth scopes granted during authorization (comma-separated).
        builder.Property(sa => sa.GrantedScopes)
            .IsRequired()
            .HasMaxLength(2000);

        // Account connection status stored as string.
        // TRD: "Active → TokenExpired → Disconnected → Revoked."
        builder.Property(sa => sa.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Instagram-specific: Personal, Creator, Business — affects available API methods.
        builder.Property(sa => sa.AccountType)
            .HasMaxLength(100);

        // Privacy flag — critical for PendingDMQueue logic.
        // TRD: "Private account → DM can only be sent after subscription."
        builder.Property(sa => sa.IsPrivateAccount)
            .IsRequired()
            .HasDefaultValue(false);

        // Cached follower count — updated once per day by a scheduled job.
        builder.Property(sa => sa.FollowersCount);

        builder.Property(sa => sa.FollowersCountUpdatedAt);

        builder.Property(sa => sa.ConnectedAt)
            .IsRequired();

        // Disconnection date — null while account is active.
        builder.Property(sa => sa.DisconnectedAt);

        // ── Ignored Properties ──────────────────────────────────────────────

        // LatestInsight is a computed navigation — not persisted as a column.
        // It is resolved at query time by fetching the most recent SocialAccountInsight.
        builder.Ignore(sa => sa.LatestInsight);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Composite unique index: one external account can only be connected once per workspace+platform.
        // Prevents duplicate connections of the same YouTube channel to the same workspace.
        builder.HasIndex(sa => new { sa.WorkspaceId, sa.Platform, sa.ExternalAccountId })
            .IsUnique()
            .HasDatabaseName("IX_SocialAccounts_Workspace_Platform_ExternalId");

        // Performance index: listing all accounts in a workspace.
        builder.HasIndex(sa => sa.WorkspaceId)
            .HasDatabaseName("IX_SocialAccounts_WorkspaceId");

        // Performance index: finding accounts by status (e.g., all TokenExpired for refresh job).
        builder.HasIndex(sa => sa.Status)
            .HasDatabaseName("IX_SocialAccounts_Status");

        // ── Relationships ───────────────────────────────────────────────────
        // One Workspace → Many SocialAccounts — configured from Workspace side.
        // Child navigations (PostTargets, Conversations, AutomationRules) are configured
        // from their respective entity configuration classes.
    }
}
