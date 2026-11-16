using Microsoft.AspNetCore.Identity;

namespace ELearningPlatform.Application.Common;

public static class IdentityResultExtensions
{
    public static string ErrorText(this IdentityResult result)
        => string.Join("\n", result.Errors.Select(e => e.Description));
}
