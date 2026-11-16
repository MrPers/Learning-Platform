using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class CourseReactionConfiguration : IEntityTypeConfiguration<CourseReaction>
{
    public void Configure(EntityTypeBuilder<CourseReaction> builder)
    {
        builder.ToTable("CourseReactions");

        builder.Property(x => x.IsLike)
            .HasColumnType("bit")
            .IsRequired();

        builder.Property(x => x.ReactedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.Rating)
            .HasConversion<byte>()
            .IsRequired(false);

        builder.HasOne(x => x.Course)
            .WithMany(v => v.CourseReactions)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany(u => u.CourseReactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
