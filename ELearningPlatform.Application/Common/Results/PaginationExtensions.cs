using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Common.Results;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(this IQueryable<T> query,
        int pageNumber, int pageSize, string baseUrl, CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber > 0 ? pageNumber : 1;
        pageSize = pageSize > 0 ? Math.Min(pageSize, MaxPageSize) : DefaultPageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            NextPage = pageNumber < totalPages ? PaginationHelper.BuildPageUrl(baseUrl, pageNumber + 1, pageSize) : null,
            PrevPage = pageNumber > 1 ? PaginationHelper.BuildPageUrl(baseUrl, pageNumber - 1, pageSize) : null,
        };
    }
}
