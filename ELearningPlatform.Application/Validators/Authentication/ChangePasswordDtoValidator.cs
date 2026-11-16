using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.")
            .MaximumLength(PasswordRules.MaxLength).WithMessage($"Password must not exceed {PasswordRules.MaxLength} characters.");

        RuleFor(x => x.NewPassword)
            .StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current one.");
    }
}
