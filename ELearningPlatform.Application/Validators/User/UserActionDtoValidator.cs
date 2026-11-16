using FluentValidation;

namespace ELearningPlatform.Application.Validators.User;

public class UserActionDtoValidator : AbstractValidator<UserActionDto>
{
    public UserActionDtoValidator()
    {
        RuleFor(x => x.UserId)
              .NotEmpty().WithMessage("UserId is required.")
              .Length(36).WithMessage("UserId must be a valid GUID with 36 characters");
    }
}
