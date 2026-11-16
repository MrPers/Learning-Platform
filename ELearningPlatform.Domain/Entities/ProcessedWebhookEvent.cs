namespace ELearningPlatform.Domain.Entities;

/// <summary>Provider events already handled. Providers deliver at least once, so duplicates are ignored.</summary>
public class ProcessedWebhookEvent
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
