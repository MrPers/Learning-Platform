namespace ELearningPlatform.Domain.Entities;

public class StudentAnswer
{
    public int ExamId { get; set; }  //for Composite PK
    public int StudentId { get; set; } //for Composite PK
    public StudentExam StudentExam { get; set; } = null!;

    public byte QuestionNumber { get; set; }
    public Question Question { get; set; } = null!;
    public byte OptionNumber { get; set; }
    public AnswerOption? AnswerOptions { get; set; }
}
