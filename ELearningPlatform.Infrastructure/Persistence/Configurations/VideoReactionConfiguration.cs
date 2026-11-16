using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class VideoReactionConfiguration : IEntityTypeConfiguration<VideoReaction>
{
    public void Configure(EntityTypeBuilder<VideoReaction> builder)
    {
        builder.ToTable("VideoReactions");

        builder.Property(x => x.IsLike)
            .HasColumnType("bit")
            .IsRequired();

        builder.Property(x => x.ReactedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne(x => x.Video)
            .WithMany(v => v.VideoReactions)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany(u => u.VideoReactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
