using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Common.Files;

public static class FileValidationHelper
{
    public static bool CheckExtension(IFormFile? file, IReadOnlyCollection<string> allowedExtensions)
    {
        if (file == null)
            return false;
        var extension = Path.GetExtension(file.FileName);
        return !string.IsNullOrEmpty(extension) && allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool CheckSize(IFormFile? file, int maxSizeInMB)
    {
        if (file == null)
            return false;
        return file.Length > 0 && file.Length <= maxSizeInMB * 1024L * 1024L;
    }

    /// <summary>
    /// Checks that the file content starts with the magic bytes of its extension, so a renamed
    /// executable or script can't be uploaded as an image or video.
    /// </summary>
    public static bool CheckSignature(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return false;

        using var stream = file.OpenReadStream();
        return FileSignatures.Matches(Path.GetExtension(file.FileName), stream);
    }
}
