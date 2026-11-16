using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(u => u.Gender)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(u => u.ProfilePictureUrl)
            .HasMaxLength(100)
            .IsRequired(false);
    }
}
