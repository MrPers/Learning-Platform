namespace ELearningPlatform.Domain.Entities;

/// <summary>Who changed what and when, with the old and new values as JSON. Append-only.</summary>
public class AuditLog
{
    public long Id { get; set; }
    /// <summary>Acting user; null for webhooks and background jobs.</summary>
    public string? UserId { get; set; }
    /// <summary>Added, Modified or Deleted.</summary>
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; }
}
