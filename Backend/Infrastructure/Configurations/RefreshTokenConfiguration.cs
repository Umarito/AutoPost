using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="RefreshToken"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures database mappings for refresh-token backed authentication sessions.</para>
/// <para><b>Business Justification:</b> Secure session management and JWT refresh cycles. Allows revoking specific device logins without affecting other sessions.
/// TRD: "Authentication Security: JWT refresh cycle, store hashed refresh tokens, track device, IP, and revocation status."</para>
/// <para><b>Execution and Project Impact:</b> Essential for user authentication integrity. Uses unique cryptographic hash indexing for fast lookups on token exchange requests.</para>
/// </remarks>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Table Name mapping
        builder.ToTable("RefreshTokens");

        // Primary Key definition
        builder.HasKey(rt => rt.Id);

        // Foreign Key to the User
        builder.Property(rt => rt.UserId)
            .IsRequired();

        // Secure SHA-256 or SHA-512 cryptographic hash of the raw token
        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(512);

        // User-agent context for security auditing and device lists
        builder.Property(rt => rt.DeviceInfo)
            .HasMaxLength(500);

        // Client IP address (handles IPv4/IPv6 up to 45 characters)
        builder.Property(rt => rt.IpAddress)
            .HasMaxLength(45);

        // Session temporal rules
        builder.Property(rt => rt.CreatedAt)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        // Status markers to enforce single-use refresh token rotation (RTR)
        builder.Property(rt => rt.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rt => rt.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        // Audit marker for revocation
        builder.Property(rt => rt.RevokedAt);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Unique index: ensures cryptographic tokens are unique and can be queried instantly during JWT exchange.
        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Foreign Key Index for UserId relationship queries
        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        // ── Relationships ───────────────────────────────────────────────────

        // Relationship: One User -> Many RefreshTokens.
        // Cascade Delete: when a user account is deleted, all their active/expired refresh tokens and sessions must be purged.
        builder.HasOne(rt => rt.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .HasConstraintName("FK_RefreshTokens_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

