namespace Reports.Contracts.Adapters;

public static class AdapterErrors
{
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string Unavailable = "UNAVAILABLE";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string Timeout = "TIMEOUT";
    public const string Unknown = "UNKNOWN";

    public static AdapterResult<T> NotFoundResult<T>(string message = "Resource not found") =>
        AdapterResult<T>.Fail(NotFound, message);

    public static AdapterResult<T> UnauthorizedResult<T>(string message = "Unauthorized") =>
        AdapterResult<T>.Fail(Unauthorized, message);

    public static AdapterResult<T> ForbiddenResult<T>(string message = "Forbidden") =>
        AdapterResult<T>.Fail(Forbidden, message);

    public static AdapterResult<T> UnavailableResult<T>(string message = "Service unavailable", bool retryable = true) =>
        AdapterResult<T>.Fail(Unavailable, message, retryable);

    public static AdapterResult<T> InvalidRequestResult<T>(string message = "Invalid request") =>
        AdapterResult<T>.Fail(InvalidRequest, message);

    public static AdapterResult<T> TimeoutResult<T>(string message = "Request timed out", bool retryable = true) =>
        AdapterResult<T>.Fail(Timeout, message, retryable);

    public static AdapterResult<T> UnknownResult<T>(string message = "An unknown error occurred") =>
        AdapterResult<T>.Fail(Unknown, message);
}
