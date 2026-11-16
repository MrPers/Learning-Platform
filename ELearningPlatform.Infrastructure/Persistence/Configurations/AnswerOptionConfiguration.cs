using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions");
        builder.HasKey(a => new { a.ExamId, a.QuestionNumber, a.OptionNumber });

        builder.Property(x => x.OptionNumber)
            .HasColumnType("TINYINT")
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.IsCorrect)
           .HasColumnType("bit")
           .IsRequired();

        builder.Property(x => x.Text)
            .HasColumnType("nvarchar")
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne(x => x.Question)
        .WithMany(q => q.AnswerOptions)
        .HasForeignKey(x => new { x.ExamId, x.QuestionNumber })
        .OnDelete(DeleteBehavior.Cascade);
    }
}
