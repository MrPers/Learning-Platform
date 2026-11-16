using FluentValidation;

namespace ELearningPlatform.Application.Validators.Authentication;

public class ExternalLoginDtoValidator : AbstractValidator<ExternalLoginDto>
{
    public ExternalLoginDtoValidator()
    {
        RuleFor(x => x.IdToken)
      .NotEmpty().WithMessage("IdToken is required");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required");
    }
}
