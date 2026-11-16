using FluentValidation;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Validators.User;

public class UpdatePictureProfileDtoValidator : AbstractValidator<UpdatePictureProfileDto>
{
    public UpdatePictureProfileDtoValidator(IOptions<UploadsSettings> uploadsSettings)
    {
        var settings = uploadsSettings.Value;

        RuleFor(x => x.PictureFile)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Picture file is required.")
            .ValidUpload("Picture", settings.ImageExtentionAllowed, settings.ImageSizeInMB);
    }
}
