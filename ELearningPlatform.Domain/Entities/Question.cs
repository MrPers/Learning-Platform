namespace ELearningPlatform.Domain.Entities;

public class Question
{
    public byte QuestionNumber { get; set; } // 1,2,3,... per exam
    public string Header { get; set; } = string.Empty; // السؤال نفسه 
    public QuestionType QuestionType { get; set; } //that will be Discriminator 
    public double Mark { get; set; }

    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public ICollection<AnswerOption> AnswerOptions { get; set; } = [];// option for each question
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = [];
}
