namespace ELearningPlatform.Application.Validators.Authentication;

using FluentValidation;
using Microsoft.Extensions.Options;

public class RegisterStudentDtoValidator : AbstractValidator<RegisterStudentDto>
{
    public RegisterStudentDtoValidator(IOptions<UploadsSettings> uploads)
    {
        Include(new RegisterDtoValidator(uploads));

        RuleFor(x => x.Level)
            .IsInEnum().WithMessage("Invalid level value.");
    }

}
