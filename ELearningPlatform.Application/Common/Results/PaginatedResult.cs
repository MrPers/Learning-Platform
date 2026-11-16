namespace ELearningPlatform.Application.Common.Results;

public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public string? NextPage { get; init; }
    public string? PrevPage { get; init; }
}
