namespace Support.Api.Middleware;

/// <summary>
/// Adds defensive security headers to every API response.
///
/// Headers applied:
///   X-Content-Type-Options: nosniff   — prevents MIME sniffing
///   X-Frame-Options: DENY             — prevents clickjacking
///   X-XSS-Protection: 0              — disables the legacy XSS filter (correct modern posture)
///   Server: (removed)                 — suppresses ASP.NET / Kestrel fingerprinting
///
/// Strict-Transport-Security and Content-Security-Policy are intentionally
/// omitted here — they are enforced at the API gateway layer.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"]        = "DENY";
            h["X-XSS-Protection"]       = "0";
            h.Remove("Server");
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
