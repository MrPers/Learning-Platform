using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class TwoFactorSendCodeDtoValidator : AbstractValidator<TwoFactorSendCodeDto>
{
    public TwoFactorSendCodeDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("invalid Email format")
            .MaximumLength(200).WithMessage("email must be less than 200 latter");

         RuleFor(x => x.Method)
            .NotNull().WithMessage("Two factor method is required")
            .IsInEnum().WithMessage("Invalid two factor method");

    }
}
