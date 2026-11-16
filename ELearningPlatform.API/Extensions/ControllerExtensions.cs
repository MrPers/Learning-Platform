namespace ELearningPlatform.API.Extensions;

public static class ControllerExtensions
{
    /// <summary>Absolute URL of the current endpoint without the query string; used for pagination links.</summary>
    public static string CurrentUrl(this ControllerBase controller)
    {
        var request = controller.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
    }

    /// <summary>Like <see cref="CurrentUrl"/> but keeps the filter parameters, so next/previous page links stay filtered.</summary>
    public static string CurrentUrlWithFilters(this ControllerBase controller)
    {
        var filters = controller.Request.Query
            .Where(q => !q.Key.Equals("pageNumber", StringComparison.OrdinalIgnoreCase) && !q.Key.Equals("pageSize", StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => KeyValuePair.Create(q.Key, v)));
        return controller.CurrentUrl() + QueryString.Create(filters);
    }
}
