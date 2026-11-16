using FluentValidation;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Validators.Video;

public class CreateVideoDtoValidator : AbstractValidator<CreateVideoDto>
{
    public CreateVideoDtoValidator(IOptions<UploadsSettings> uploadsSettings)
    {
        var settings = uploadsSettings.Value;

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Video Title is required")
            .MaximumLength(500).WithMessage("Video Title must not exceed 500 characters.");

        RuleFor(x => x.OriginalName)
            .NotEmpty().WithMessage("Video Name is required")
            .MaximumLength(200).WithMessage("Original Name Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("Video Description must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.VideoFile)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Video file is required.")
            .ValidUpload("Video", settings.VideoExtentionAllowed, settings.VideoSizeInMB);
    }
}
