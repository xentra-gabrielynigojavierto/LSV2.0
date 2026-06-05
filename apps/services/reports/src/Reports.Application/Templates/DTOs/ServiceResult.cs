namespace Reports.Application.Templates.DTOs;

public sealed class ServiceResult<T>
{
    public bool Success { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int StatusCode { get; private init; }

    public static ServiceResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
        StatusCode = 200
    };

    public static ServiceResult<T> Created(T data) => new()
    {
        Success = true,
        Data = data,
        StatusCode = 201
    };

    public static ServiceResult<T> NotFound(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 404
    };

    public static ServiceResult<T> BadRequest(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 400
    };

    public static ServiceResult<T> Conflict(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 409
    };

    public static ServiceResult<T> Forbidden(string message = "Access denied.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 403
    };

    public static ServiceResult<T> Fail(string message, int statusCode = 500) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = statusCode
    };
}
