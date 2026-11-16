namespace ELearningPlatform.Domain.Entities;

/// <summary>
/// The response to a POST sent with an Idempotency-Key, kept so that a retried request gets the same
/// result instead of charging twice. Keys are scoped per user.
/// </summary>
public class IdempotencyRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    /// <summary>SHA-256 of method, route and arguments; reusing a key for a different request is rejected.</summary>
    public string RequestHash { get; set; } = string.Empty;
    /// <summary>Null while the first request is still running.</summary>
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
