namespace ELearningPlatform.Application.Common.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>Success message, or the error description on failure.</summary>
    public string Message { get; }
    public Error Error { get; }
    public ErrorType ErrorType => Error.Type;

    protected Result(bool isSuccess, string message, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
        if (!isSuccess && error == Error.None)
            throw new ArgumentException("A failed result must carry an error.", nameof(error));

        IsSuccess = isSuccess;
        Message = message;
        Error = error;
    }

    public static Result Success(string message = "") => new(true, message, Error.None);

    public static Result Fail(Error error) => new(false, error.Description, error);
    public static Result Fail(string message, ErrorType errorType = ErrorType.Failure, string? code = null)
        => Fail(new Error(code ?? Error.DefaultCode(errorType), message, errorType));
    public static Result NotFound(string message, string? code = null) => Fail(Error.NotFound(message, code));
    public static Result Validation(string message, string? code = null) => Fail(Error.Validation(message, code));
    public static Result Conflict(string message, string? code = null) => Fail(Error.Conflict(message, code));
    public static Result Unauthorized(string message, string? code = null) => Fail(Error.Unauthorized(message, code));
    public static Result Forbidden(string message, string? code = null) => Fail(Error.Forbidden(message, code));
}
