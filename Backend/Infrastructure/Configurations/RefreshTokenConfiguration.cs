using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// Configures refresh-token backed sessions.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId).IsRequired();
        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(512);
        builder.Property(rt => rt.DeviceInfo).HasMaxLength(500);
        builder.Property(rt => rt.IpAddress).HasMaxLength(45);
        builder.Property(rt => rt.CreatedAt).IsRequired();
        builder.Property(rt => rt.ExpiresAt).IsRequired();
        builder.Property(rt => rt.IsUsed).IsRequired().HasDefaultValue(false);
        builder.Property(rt => rt.IsRevoked).IsRequired().HasDefaultValue(false);
        builder.Property(rt => rt.RevokedAt);

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasOne(rt => rt.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .HasConstraintName("FK_RefreshTokens_Users_UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
