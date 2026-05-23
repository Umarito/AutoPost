// Infrastructure/Configurations/ApplicationUserConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the ApplicationUser entity.
/// ApplicationUser extends IdentityUser&lt;Guid&gt;, so Identity-managed columns (Email, PasswordHash, etc.)
/// are NOT configured here — they are handled by IdentityDbContext.
/// This configuration focuses exclusively on custom domain properties and navigation relationships.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // NOTE: No ToTable() call — IdentityDbContext manages the AspNetUsers table.
        // Adding ToTable() here would conflict with Identity's own table mapping.

        // ── Custom Properties ───────────────────────────────────────────────

        // Display name shown in workspace UI, inbox assignments, and team views.
        // TRD: "Отображаемое имя в интерфейсе" — separate from Identity's UserName.
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        // Avatar URL for visual identification in collaborative features.
        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(2048);

        // IANA timezone string (e.g., "Asia/Dushanbe") for UI time display.
        // TRD: "All dates inside the system are in UTC" — timezone is display-only.
        builder.Property(u => u.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("UTC");

        // UI language preference: "ru", "en", etc.
        builder.Property(u => u.Locale)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("en");

        // Registration timestamp for onboarding audit and lifecycle tracking.
        builder.Property(u => u.RegisteredAt)
            .IsRequired();

        // Last login timestamp — useful for security audit and activity monitoring.
        builder.Property(u => u.LastLoginAt);

        // Business-level active flag, separate from Identity's LockoutEnabled.
        // TRD: "Deactivated user cannot log in and perform actions."
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // ── Relationships ───────────────────────────────────────────────────

        // One User → Many WorkspaceMemberships.
        // TRD: "User can be a member of multiple Workspaces with different roles."
        // Configured from WorkspaceMember side (see WorkspaceMemberConfiguration).

        // One User → Many RefreshTokens.
        // TRD: "Active sessions, used for 'log out from all devices'."
        // Configured from RefreshToken side (see RefreshTokenConfiguration).

        // One User → Many NotificationPreferences.
        // TRD: "Per-user notification settings across workspaces."
        // Configured from NotificationPreference side (see NotificationPreferenceConfiguration).
    }
}
