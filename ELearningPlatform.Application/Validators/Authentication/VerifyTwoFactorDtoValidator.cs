using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class VerifyTwoFactorDtoValidator : AbstractValidator<VerifyTwoFactorDto>
{
    public VerifyTwoFactorDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("invalid Email format")
            .MaximumLength(200).WithMessage("email must be less than 200 latter");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required")
            .Length(6).WithMessage("Code must be exactly 6 digits")
            .Matches("^[0-9]{6}$").WithMessage("Code must contain only digits");

        RuleFor(x => x.Method)
        .NotEmpty().WithMessage("Method is required")
        .IsInEnum().WithMessage("Method must be Email or SMS ");
    }
}
