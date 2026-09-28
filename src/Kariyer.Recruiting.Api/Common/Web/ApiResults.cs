using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Api.Common.Web;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string InternalError = "INTERNAL_ERROR";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
}

public sealed record ApiError(ApiErrorBody Error)
{
    public static ApiError Create(string code, string message, object? details = null) =>
        new(new ApiErrorBody(code, message, details));
}

public sealed record ApiErrorBody(string Code, string Message, object? Details);

/// <summary>The error envelope the technical document specifies (§5.1).</summary>
public static class ApiResults
{
    public static IResult Validation(ValidationResult validation) => Results.Json(
        ApiError.Create(ErrorCodes.ValidationError, "Eksik veya hatalı alanları kontrol edin.", validation.Errors),
        statusCode: StatusCodes.Status400BadRequest);

    public static IResult Validation(string field, string message) => Results.Json(
        ApiError.Create(
            ErrorCodes.ValidationError,
            "Eksik veya hatalı alanları kontrol edin.",
            new Dictionary<string, string[]> { [field] = [message] }),
        statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string message = "Başvuru bulunamadı veya erişiminiz yok.") => Results.Json(
        ApiError.Create(ErrorCodes.NotFound, message), statusCode: StatusCodes.Status404NotFound);

    public static IResult Forbidden(string message = "Bu kayıt üzerinde işlem yapma yetkiniz yok.") => Results.Json(
        ApiError.Create(ErrorCodes.Forbidden, message), statusCode: StatusCodes.Status403Forbidden);

    public static IResult Unauthorized(string message = "Oturum süreniz dolmuş olabilir.") => Results.Json(
        ApiError.Create(ErrorCodes.Unauthorized, message), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult InvalidTransition(InvalidStageTransitionException exception) => Results.Json(
        ApiError.Create(
            ErrorCodes.InvalidStatusTransition,
            "Adayın mevcut durumu bu işleme uygun değil.",
            new { from = exception.From, to = exception.To }),
        statusCode: StatusCodes.Status409Conflict);

    public static IResult Conflict(string message) => Results.Json(
        ApiError.Create(ErrorCodes.InvalidStatusTransition, message), statusCode: StatusCodes.Status409Conflict);
}
