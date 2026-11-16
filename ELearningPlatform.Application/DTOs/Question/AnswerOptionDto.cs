namespace ELearningPlatform.Application.DTOs.Question;

public class AnswerOptionDto
{
    public byte QuestionNumber { get; set; }
    public byte OptionNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
