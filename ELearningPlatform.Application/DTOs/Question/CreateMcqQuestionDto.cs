namespace ELearningPlatform.Application.DTOs.Question;

public class CreateMcqQuestionDto : CreateQuestionDto
{
    public string Option1 { get; set; } = string.Empty;
    public string Option2 { get; set; } = string.Empty;
    public string Option3 { get; set; } = string.Empty;
    public string Option4 { get; set; } = string.Empty;
    public int CorrectAnswerIndex { get; set; } // 1, 2, 3, 4

}
