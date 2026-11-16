namespace ELearningPlatform.Application.DTOs.Exam;

/// <summary>An exam as a student sees it while taking it: questions and options, never the correct answers.</summary>
public class ExamPaperDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int DurationInMinutes { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public List<ExamPaperQuestionDto> Questions { get; set; } = [];
}

public class ExamPaperQuestionDto
{
    public byte QuestionNumber { get; set; }
    public string Header { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public double Mark { get; set; }
    public List<ExamPaperOptionDto> Options { get; set; } = [];
}

public class ExamPaperOptionDto
{
    public byte OptionNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}
