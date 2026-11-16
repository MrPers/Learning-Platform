using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class EnableTwoFactorDtoValidator : AbstractValidator<EnableTwoFactorDto>
{
    public EnableTwoFactorDtoValidator()
    {
        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("Method is required")
            .IsInEnum().WithMessage("Method must be Email or SMS ");
    }
}
