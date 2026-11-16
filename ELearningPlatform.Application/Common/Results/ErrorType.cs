namespace ELearningPlatform.Application.Common.Results;

/// <summary>Kind of failure; the API maps each one to an HTTP status code.</summary>
public enum ErrorType
{
    None = 0,
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    /// <summary>A dependency (e.g. the payment provider) failed; the client may retry.</summary>
    Unavailable,
}
