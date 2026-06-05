using System.Text.RegularExpressions;

namespace CareConnect.Api.Middleware;

/// <summary>
/// BLK-OBS-01: Correlation ID middleware.
///
/// Reads X-Correlation-Id from the incoming request.
/// If the header is absent or invalid, generates a new GUID.
/// Stores the resolved value in HttpContext.Items["CorrelationId"] for downstream use.
/// Echoes the value in the X-Correlation-Id response header so callers can trace replies.
///
/// Convention aligns with the Audit, Documents, and Reports service middleware already
/// deployed on the platform.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName  = "X-Correlation-Id";
    private const int    MaxLength   = 100;
    private static readonly Regex SafePattern = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

        var correlationId =
            !string.IsNullOrWhiteSpace(incoming)
            && incoming.Length <= MaxLength
            && SafePattern.IsMatch(incoming)
                ? incoming
                : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
