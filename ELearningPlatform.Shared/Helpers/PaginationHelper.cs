namespace ELearningPlatform.Shared.Helpers;

public static class PaginationHelper
{
    public static string BuildPageUrl(string baseUrl, int pageNumber, int pageSize)
    {
        // baseUrl may already carry filters (?status=...)
        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}pageNumber={pageNumber}&pageSize={pageSize}";
    }
}
