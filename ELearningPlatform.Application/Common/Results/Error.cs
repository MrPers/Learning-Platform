namespace ELearningPlatform.Application.Common.Results;

/// <summary>
/// Machine-readable failure. <see cref="Code"/> is stable and safe for clients to switch on;
/// the API returns it as the "code" extension of an RFC 9457 problem details response.
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Failure(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.Failure), description, ErrorType.Failure);
    public static Error Validation(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.Validation), description, ErrorType.Validation);
    public static Error NotFound(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.NotFound), description, ErrorType.NotFound);
    public static Error Conflict(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.Conflict), description, ErrorType.Conflict);
    public static Error Unauthorized(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.Unauthorized), description, ErrorType.Unauthorized);
    public static Error Forbidden(string description, string? code = null) => new(code ?? DefaultCode(ErrorType.Forbidden), description, ErrorType.Forbidden);

    public static string DefaultCode(ErrorType type) => $"General.{type}";
}
