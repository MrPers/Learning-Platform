namespace ELearningPlatform.Application.Validators.Authentication;

using FluentValidation;
using Microsoft.Extensions.Options;

public class RegisterTeacherDtoValidator : AbstractValidator<RegisterTeacherDto>
{
    public RegisterTeacherDtoValidator(IOptions<UploadsSettings> uploads)
    {
        Include(new RegisterDtoValidator(uploads));

        RuleFor(x => x.Specialization)
            .IsInEnum().WithMessage("Invalid specialization value.");
    }
}