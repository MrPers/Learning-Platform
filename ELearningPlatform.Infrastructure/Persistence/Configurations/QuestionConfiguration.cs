using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(p => new { p.ExamId, p.QuestionNumber });

        builder.Property(q => q.QuestionNumber)
            .HasColumnType("TINYINT")
            .IsRequired();

        builder.Property(p => p.Header)
            .HasColumnType("nvarchar")
            .HasMaxLength(1000)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(p => p.QuestionType)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(p => p.Mark)
           .HasColumnType("float")
           .IsRequired();

        builder.HasOne(x => x.Exam)
            .WithMany(x => x.Questions)
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
