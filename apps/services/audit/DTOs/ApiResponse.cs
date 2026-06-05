namespace PlatformAuditEventService.DTOs;

/// <summary>
/// Standardized API response envelope for all endpoints.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool   Success   { get; init; }
    public T?     Data      { get; init; }
    public string? Message  { get; init; }
    public string? TraceId  { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ApiResponse<T> Ok(T data, string? message = null, string? traceId = null) =>
        new() { Success = true, Data = data, Message = message, TraceId = traceId };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors ?? [], TraceId = traceId };

    public static ApiResponse<T> ValidationFail(IReadOnlyList<string> errors, string? traceId = null) =>
        new() { Success = false, Message = "Validation failed.", Errors = errors, TraceId = traceId };
}

public static class ApiResponse
{
    public static ApiResponse<object> Ok(string? message = null, string? traceId = null) =>
        ApiResponse<object>.Ok(new { }, message, traceId);

    public static ApiResponse<object> Fail(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        ApiResponse<object>.Fail(message, errors, traceId);
}
