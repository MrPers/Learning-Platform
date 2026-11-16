using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Validators;

public static class FileRules
{
    /// <summary>Allowed extension, size limit, and content that really is of that type (magic bytes).</summary>
    public static IRuleBuilderOptions<T, IFormFile?> ValidUpload<T>(this IRuleBuilder<T, IFormFile?> rule,
        string label, IReadOnlyCollection<string> allowedExtensions, int maxSizeInMB) => rule
        .Must(file => FileValidationHelper.CheckExtension(file, allowedExtensions))
        .WithMessage($"{label} must be {string.Join(',', allowedExtensions)} format.")
        .Must(file => FileValidationHelper.CheckSize(file, maxSizeInMB))
        .WithMessage($"{label} size must not exceed {maxSizeInMB} MB.")
        .Must(FileValidationHelper.CheckSignature)
        .WithMessage($"{label} content does not match its file type.");
}
