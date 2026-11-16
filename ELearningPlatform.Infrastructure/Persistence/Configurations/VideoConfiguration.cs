using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("Videos");
        builder.HasKey(p => p.Id);
        //builder.HasKey(x => new { x.Id, x.CourseId });

        builder.Property(p => p.Title)
            .HasColumnType("nvarchar")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnType("nvarchar")
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(p => p.OriginalName)
            .HasColumnType("nvarchar")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.VideoUrl)
            .HasColumnType("nvarchar")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ContentType)
            .HasColumnType("nvarchar")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.UploadedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne(x => x.Course)
            .WithMany(x => x.Videos)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

    }
}
