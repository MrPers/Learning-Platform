namespace ELearningPlatform.Application.Abstractions.Services;

/// <summary>The caller of the current request, for audit records. All values are null outside an HTTP request.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? IpAddress { get; }
    string? TraceId { get; }
}
