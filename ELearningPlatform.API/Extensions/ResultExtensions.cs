namespace ELearningPlatform.API.Extensions;

/// <summary>Turns application results into HTTP responses; failures become RFC 9457 problem details.</summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
        => result.IsSuccess ? controller.Ok(new MessageResponse(result.Message)) : result.ToProblem(controller);

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
        => result.IsSuccess ? controller.Ok(result.Data) : result.ToProblem(controller);

    public static IActionResult ToProblem(this Result result, ControllerBase controller)
        => controller.Problem(
            detail: result.Error.Description,
            statusCode: StatusCodeFor(result.Error.Type),
            title: TitleFor(result.Error.Type),
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unavailable => "Service unavailable",
        _ => "Request failed",
    };
}

public sealed record MessageResponse(string Message);
