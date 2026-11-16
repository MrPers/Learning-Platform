namespace ELearningPlatform.Application.DTOs.User;

public class TeacherDto : UserDto
{
    public Specialization Specialization { get; set; }
    public Role Role => Role.Teacher;
}
