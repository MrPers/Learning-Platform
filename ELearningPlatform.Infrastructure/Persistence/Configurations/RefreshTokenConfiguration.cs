using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);

        // Hex SHA-256 = 64 chars; looked up on every refresh, so indexed
        builder.Property(x => x.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.RevokedAt)
            .HasColumnType("datetime2")
            .IsRequired(false);

        builder.Property(x => x.ExpiresOn)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne(x => x.ApplicationUser)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
