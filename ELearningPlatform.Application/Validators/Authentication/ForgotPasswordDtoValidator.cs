using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator()
    {
        RuleFor(x => x.Email)
               .NotEmpty().WithMessage("Email is required")
               .EmailAddress().WithMessage("invalid Email format")
               .MaximumLength(200).WithMessage("email must be less than 200 latter");
    }
}
