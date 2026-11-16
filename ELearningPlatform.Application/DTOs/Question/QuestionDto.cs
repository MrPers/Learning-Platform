namespace ELearningPlatform.Application.DTOs.Question;

public class QuestionDto
{
    public byte QuestionNumber { get; set; }
    public string Header { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public double Mark { get; set; }
    public int ExamId { get; set; }

    public ICollection<AnswerOptionDto> AnswerOptions { get; set; } = new List<AnswerOptionDto>();
}
