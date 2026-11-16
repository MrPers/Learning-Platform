using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("invalid Email format")
            .MaximumLength(200).WithMessage("email must be less than 200 latter");

        // No policy checks here: passwords set under an older policy must still be able to sign in
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MaximumLength(PasswordRules.MaxLength).WithMessage($"Password must not exceed {PasswordRules.MaxLength} characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("invalid role");
    }
}
