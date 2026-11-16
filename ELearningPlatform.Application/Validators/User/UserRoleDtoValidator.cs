using FluentValidation;

namespace ELearningPlatform.Application.Validators.User;

public class UserRoleDtoValidator : AbstractValidator<UserRoleDto>
{
    public UserRoleDtoValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role. Allowed roles are Admin, Teacher, Student.");

        RuleFor(x => x.UserId)
              .NotEmpty().WithMessage("UserId is required.")
              .Length(36).WithMessage("UserId must be a valid GUID with 36 characters");
    }
}
