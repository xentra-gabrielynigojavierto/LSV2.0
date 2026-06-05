using Serilog.Context;
using System.Text.RegularExpressions;

namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Reads X-Correlation-ID from the incoming request and writes it back to the response.
/// If absent or invalid, generates a new correlation ID.
///
/// Security hardening (Step 21):
///   - Incoming values are validated: max 100 chars, alphanumeric / hyphen / underscore only.
///     Any value that fails this check is discarded and a fresh GUID is generated.
///     This prevents oversized values and special-character header injection from reaching
///     response headers, logs, and downstream systems.
///   - The resolved correlation ID is pushed into Serilog's LogContext so it appears
///     automatically in every structured log entry written during this request.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName              = "X-Correlation-ID";
    private const int    MaxCorrelationIdLength  = 100;

    // Only allow characters that are safe in HTTP header values and structured log fields.
    // Accepts UUIDs (hex + hyphens), short slugs, and common trace-ID formats.
    private static readonly Regex SafePattern = new(
        @"^[a-zA-Z0-9\-_]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ResolveCorrelationId(ctx.Request.Headers[HeaderName].FirstOrDefault());

        ctx.Items["CorrelationId"]       = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        // Push into Serilog's log context so every log entry written during this
        // request automatically includes CorrelationId as a structured property.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }

    /// <summary>
    /// Validates the incoming correlation ID header value.
    /// Returns the sanitised value when safe, or a freshly generated GUID when absent or invalid.
    /// </summary>
    private static string ResolveCorrelationId(string? incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming)
            && incoming.Length <= MaxCorrelationIdLength
            && SafePattern.IsMatch(incoming))
        {
            return incoming;
        }

        return Guid.NewGuid().ToString("D");
    }
}
