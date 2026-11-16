using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class UploadsSettings : IValidatableObject
{
    [Range(1, 100)]
    public int ImageSizeInMB { get; set; }

    [Range(1, 10_000)]
    public int VideoSizeInMB { get; set; }

    [Range(1, 1_000)]
    public int FileSizeInMB { get; set; }

    [Required, MinLength(1)]
    public List<string> ImageExtentionAllowed { get; set; } = [];

    [Required, MinLength(1)]
    public List<string> VideoExtentionAllowed { get; set; } = [];

    public List<string> FileExtentionAllowed { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, list) in new[]
        {
            (nameof(ImageExtentionAllowed), ImageExtentionAllowed),
            (nameof(VideoExtentionAllowed), VideoExtentionAllowed),
            (nameof(FileExtentionAllowed), FileExtentionAllowed),
        })
        {
            if (list.Any(e => string.IsNullOrWhiteSpace(e) || !e.StartsWith('.')))
                yield return new ValidationResult($"Uploads:{name} entries must be extensions like \".png\".", [name]);
        }
    }
}
