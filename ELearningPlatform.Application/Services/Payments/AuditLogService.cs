using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Payments;

public interface IAuditLogService
{
    Task<Result<PaginatedResult<AuditLogDto>>> GetAsync(AuditLogQuery query, string baseUrl, CancellationToken cancellationToken = default);
}

public sealed class AuditLogService(IAppDbContext db) : IAuditLogService
{
    public async Task<Result<PaginatedResult<AuditLogDto>>> GetAsync(AuditLogQuery query, string baseUrl, CancellationToken cancellationToken = default)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            logs = logs.Where(a => a.EntityType == query.EntityType);
        if (!string.IsNullOrWhiteSpace(query.EntityId))
            logs = logs.Where(a => a.EntityId == query.EntityId);
        if (!string.IsNullOrWhiteSpace(query.UserId))
            logs = logs.Where(a => a.UserId == query.UserId);
        if (query.From is { } from)
            logs = logs.Where(a => a.Timestamp >= from);
        if (query.To is { } to)
            logs = logs.Where(a => a.Timestamp < to);

        var page = await logs.OrderByDescending(a => a.Id)
            .Select(PaymentMappings.AuditLogToDto)
            .ToPaginatedResultAsync(query.PageNumber, query.PageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<AuditLogDto>>.Success(page);
    }
}
