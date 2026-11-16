namespace ELearningPlatform.Domain.Entities;

public class AnswerOption
{
    public byte OptionNumber { get; set; } // 1,2,3,4 for mcq  or 1,2 for tf
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }

    public int ExamId { get; set; }
    public byte QuestionNumber { get; set; }
    public Question Question { get; set; } = null!;
}
