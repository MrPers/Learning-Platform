namespace ELearningPlatform.Application.DTOs.Media;

public class VideoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string CourseName { get; set; } = null!;
    public string TeacherName { get; set; } = null!;
    public int NumberOfLike { get; set; }
    public int NumberOfDislike { get; set; }
    public int NumberOfComment { get; set; }



    //// for result
    //[JsonIgnore]
    //public bool Success { get; set; }
    //[JsonIgnore]
    //public string Message { get; set; } = string.Empty;

}
