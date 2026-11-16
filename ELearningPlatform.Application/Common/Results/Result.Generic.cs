namespace ELearningPlatform.Application.Common.Results;

public class Result<T> : Result
{
    public T? Data { get; }

    private Result(T? data, bool isSuccess, string message, Error error)
        : base(isSuccess, message, error)
    {
        Data = isSuccess ? data : default;
    }

    public static Result<T> Success(T data, string message = "") => new(data, true, message, Error.None);

    public static new Result<T> Fail(Error error) => new(default, false, error.Description, error);
    public static new Result<T> Fail(string message, ErrorType errorType = ErrorType.Failure, string? code = null)
        => Fail(new Error(code ?? Error.DefaultCode(errorType), message, errorType));
    public static new Result<T> NotFound(string message, string? code = null) => Fail(Error.NotFound(message, code));
    public static new Result<T> Validation(string message, string? code = null) => Fail(Error.Validation(message, code));
    public static new Result<T> Conflict(string message, string? code = null) => Fail(Error.Conflict(message, code));
    public static new Result<T> Unauthorized(string message, string? code = null) => Fail(Error.Unauthorized(message, code));
    public static new Result<T> Forbidden(string message, string? code = null) => Fail(Error.Forbidden(message, code));

    /// <summary>Carries a failure from another result into this result type.</summary>
    public static Result<T> From(Result failure) => failure.IsSuccess
        ? throw new ArgumentException("Only failed results can be converted.", nameof(failure))
        : Fail(failure.Error);
}
