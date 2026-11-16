namespace ELearningPlatform.Application.DTOs.User;

public class StudentDto : UserDto
{
    public Level Level { get; set; }
    public Role Role => Role.Student;

}
