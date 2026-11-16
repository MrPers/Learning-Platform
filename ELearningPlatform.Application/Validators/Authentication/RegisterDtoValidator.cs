using FluentValidation;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Validators.Authentication;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator(IOptions<UploadsSettings> uploads)
    {
        var settings = uploads.Value;

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First Name is required")
            .MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last Name is required")
            .MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone Number is required")
            .Matches(@"^\+20(10|11|12|15)[0-9]{8}$").WithMessage("Invalid Phone Number format ,must be Egyptian number")
            .MaximumLength(13).WithMessage("Phone number must be less than 13 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

        RuleFor(x => x.Gender)
            .IsInEnum().WithMessage("Invalid gender value.");

        RuleFor(x => x.Password)
            .StrongPassword();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.ProfilePictureFile)
            .ValidUpload("Profile picture", settings.ImageExtentionAllowed, settings.ImageSizeInMB)
            .When(x => x.ProfilePictureFile != null);
    }
}
