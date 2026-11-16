using FluentValidation;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Validators.Video;

public class UpdateVideoFileDtoValidator : AbstractValidator<UpdateVideoFileDto>
{
    public UpdateVideoFileDtoValidator(IOptions<UploadsSettings> uploadsSettings)
    {
        var settings = uploadsSettings.Value;

        RuleFor(x => x.VideoFile)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Video file is required.")
            .ValidUpload("Video", settings.VideoExtentionAllowed, settings.VideoSizeInMB);
    }
}
