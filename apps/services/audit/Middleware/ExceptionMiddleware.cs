using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Centralized exception handler. Catches unhandled exceptions, logs them,
/// and returns a structured JSON error response using the ApiResponse envelope.
/// Must be registered as the first middleware in the pipeline.
///
/// Security hardening (Step 21):
///   - Internal exception messages are NEVER forwarded to clients. All messages in
///     the response body are static, caller-safe strings. Stack traces and internal
///     details remain in server logs only.
///   - UnauthorizedAccessException maps to HTTP 403 Forbidden (not 401 Unauthorized).
///     In .NET semantics, UnauthorizedAccessException means "you are known but not
///     permitted" — an authorization failure — which is correctly expressed as 403.
///     HTTP 401 is reserved for authentication failures (no or invalid credentials).
///   - ArgumentException and InvalidOperationException map to HTTP 400 with a
///     generic message; the exception detail remains in the Warning-level server log.
///   - The JSON serializer includes JsonStringEnumConverter, matching the controller
///     pipeline serializer so all response bodies are consistent.
///   - Client errors (4xx) are logged at Warning; server faults (5xx) at Error.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate             _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
        Converters           = { new JsonStringEnumConverter() },
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var traceId = TraceIdAccessor.Current() ?? ctx.TraceIdentifier;

        var (statusCode, safeMessage) = ex switch
        {
            // 400 — bad request. Use static, caller-safe messages; internal detail stays in the log.
            ArgumentException         => (HttpStatusCode.BadRequest, "Invalid input."),
            InvalidOperationException => (HttpStatusCode.BadRequest, "The request could not be processed in the current state."),

            // 403 — access denied. UnauthorizedAccessException in .NET means the caller is
            // known but lacks permission: that is Forbidden, NOT Unauthorized (401).
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied."),

            // 404 — resource not found.
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),

            // 5xx — unexpected fault. Never expose internal exception details to callers.
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
        };

        // Client errors (4xx): Warning. Server faults (5xx): Error.
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}. TraceId={TraceId}",
                ctx.Request.Method, ctx.Request.Path, traceId);
        else
            _logger.LogWarning(ex,
                "Request rejected with HTTP {StatusCode} on {Method} {Path}. TraceId={TraceId}",
                (int)statusCode, ctx.Request.Method, ctx.Request.Path, traceId);

        var response = ApiResponse<object>.Fail(safeMessage, traceId: traceId);

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode  = (int)statusCode;

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(response, _json));
    }
}
