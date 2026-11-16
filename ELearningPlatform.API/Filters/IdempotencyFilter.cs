using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELearningPlatform.Application.Abstractions.Persistence;
using ELearningPlatform.Domain.Entities;
using ELearningPlatform.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.API.Filters;

/// <summary>
/// Requires an <c>Idempotency-Key</c> header and makes the action run at most once per (user, key):
/// a retry of the same request gets the stored response (with <c>Idempotent-Replayed: true</c>), the same key
/// with a different request is rejected (422), and a retry while the first request is still running gets 409.
/// Responses with status 5xx are not stored, so the client may retry them with the same key.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));

public sealed class IdempotencyFilter(IAppDbContext db, TimeProvider timeProvider, IOptions<JsonOptions> jsonOptions, ILogger<IdempotencyFilter> logger)
    : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public const string KeyMissing = "Idempotency.KeyMissing";
    public const string KeyInvalid = "Idempotency.KeyInvalid";
    public const string KeyReused = "Idempotency.KeyReused";
    public const string InProgress = "Idempotency.RequestInProgress";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = (ControllerBase)context.Controller;
        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;

        var key = http.Request.Headers[HeaderName].ToString().Trim();
        if (key.Length == 0)
        {
            context.Result = Problem(controller, StatusCodes.Status400BadRequest, KeyMissing, $"The {HeaderName} header is required for this request.");
            return;
        }
        if (key.Length > IdempotencyRecordConfiguration.MaxKeyLength || key.Any(char.IsControl))
        {
            context.Result = Problem(controller, StatusCodes.Status400BadRequest, KeyInvalid, $"The {HeaderName} must be at most {IdempotencyRecordConfiguration.MaxKeyLength} printable characters.");
            return;
        }

        var userId = http.User.GetUserId();
        var requestHash = HashRequest(context);

        var existing = await FindAsync(userId, key, cancellationToken);
        if (existing != null)
        {
            context.Result = Replay(controller, http, existing, requestHash);
            return;
        }

        // Claim the key; the unique index on (UserId, Key) decides between two concurrent first requests
        var record = new IdempotencyRecord { UserId = userId, Key = key, RequestHash = requestHash, CreatedAt = Now };
        db.IdempotencyRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await FindAsync(userId, key, cancellationToken);
            context.Result = existing != null
                ? Replay(controller, http, existing, requestHash)
                : Problem(controller, StatusCodes.Status409Conflict, InProgress, "A request with this idempotency key is already being processed.");
            return;
        }
        finally
        {
            // Not part of the action's unit of work: it is completed with ExecuteUpdate below
            if (db.ChangeTracker.Entries<IdempotencyRecord>().FirstOrDefault(e => e.Entity == record) is { } entry)
                entry.State = EntityState.Detached;
        }

        var executed = await next();

        var (statusCode, body, contentType) = Capture(executed);
        if (executed.Exception != null && !executed.ExceptionHandled || statusCode >= 500)
        {
            // Not a final answer: free the key so the client can retry
            await db.IdempotencyRecords.Where(r => r.Id == record.Id).ExecuteDeleteAsync(CancellationToken.None);
            return;
        }

        await db.IdempotencyRecords.Where(r => r.Id == record.Id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.StatusCode, statusCode)
            .SetProperty(r => r.ResponseBody, body)
            .SetProperty(r => r.ResponseContentType, contentType)
            .SetProperty(r => r.CompletedAt, Now), CancellationToken.None);
    }

    private Task<IdempotencyRecord?> FindAsync(string userId, string key, CancellationToken cancellationToken)
        => db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);

    private IActionResult Replay(ControllerBase controller, HttpContext http, IdempotencyRecord record, string requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(record.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
            return Problem(controller, StatusCodes.Status422UnprocessableEntity, KeyReused, "This idempotency key was already used for a different request.");

        if (record.StatusCode is not { } statusCode)
            return Problem(controller, StatusCodes.Status409Conflict, InProgress, "A request with this idempotency key is already being processed.");

        logger.LogInformation("Replayed idempotent response for key of user {UserId}", record.UserId);
        http.Response.Headers[ReplayedHeader] = "true";
        return record.ResponseBody == null
            ? new StatusCodeResult(statusCode)
            : new ContentResult { StatusCode = statusCode, Content = record.ResponseBody, ContentType = record.ResponseContentType };
    }

    private (int StatusCode, string? Body, string? ContentType) Capture(ActionExecutedContext executed)
    {
        switch (executed.Result)
        {
            case ObjectResult { Value: not null } result:
                var body = JsonSerializer.Serialize(result.Value, result.Value.GetType(), jsonOptions.Value.JsonSerializerOptions);
                var contentType = result.Value is ProblemDetails ? "application/problem+json" : "application/json";
                return (result.StatusCode ?? StatusCodes.Status200OK, body, contentType);
            case ObjectResult result:
                return (result.StatusCode ?? StatusCodes.Status204NoContent, null, null);
            case IStatusCodeActionResult { StatusCode: { } status }:
                return (status, null, null);
            default:
                return (executed.Exception != null ? StatusCodes.Status500InternalServerError : StatusCodes.Status200OK, null, null);
        }
    }

    /// <summary>Same route and arguments = same request. CancellationTokens and the like are not part of it.</summary>
    private string HashRequest(ActionExecutingContext context)
    {
        var arguments = context.ActionArguments
            .Where(a => a.Value is not CancellationToken)
            .OrderBy(a => a.Key, StringComparer.Ordinal)
            .ToDictionary(a => a.Key, a => a.Value);
        var canonical = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}\n"
                        + JsonSerializer.Serialize(arguments, jsonOptions.Value.JsonSerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static ObjectResult Problem(ControllerBase controller, int status, string code, string detail)
        => (ObjectResult)controller.Problem(detail: detail, statusCode: status, title: "Idempotency",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
