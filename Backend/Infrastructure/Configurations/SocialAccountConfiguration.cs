// Infrastructure/Configurations/SocialAccountConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="SocialAccount"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures schema mappings for connected social platform accounts.</para>
/// <para><b>Business Justification:</b> Bridges the system with external platforms (YouTube, Instagram, TikTok, etc.).
/// TRD: "OAuth tokens are stored ENCRYPTED via IDataProtectionProvider. Plaintext NEVER reaches DB, logs, or API responses."</para>
/// <para><b>Execution and Project Impact:</b> Essential for core system functionality. Handles token encryption safeguards, platform type constraints, and indexes to support OAuth token refresh loops.</para>
/// </remarks>
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

        // LatestInsight is a computed navigation property and must not be persisted as a database column.
        // Omission Reason: It represents a dynamic context (the most recent SocialAccountInsight snapshot),
        // which changes over time and is resolved efficiently at query time using LINQ projections rather than 
        // storing a physical, redundantly managed foreign key column pointing to the insights table.
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

