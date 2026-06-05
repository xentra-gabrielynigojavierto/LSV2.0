using System.Diagnostics;

namespace Reports.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _log;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                         ?? ctx.TraceIdentifier;

        var tenantId = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown";

        ctx.Response.Headers["X-Correlation-Id"] = correlationId;

        ctx.Items["CorrelationId"] = correlationId;
        ctx.Items["TenantId"] = tenantId;

        var sw = Stopwatch.StartNew();

        using (_log.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TenantId"]      = tenantId,
            ["RequestPath"]   = ctx.Request.Path.ToString(),
            ["Method"]        = ctx.Request.Method,
        }))
        {
            _log.LogInformation("→ {Method} {Path} tenant={TenantId}", ctx.Request.Method, ctx.Request.Path, tenantId);

            await _next(ctx);

            sw.Stop();
            _log.LogInformation("← {Method} {Path} responded {StatusCode} in {ElapsedMs}ms tenant={TenantId}",
                ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds, tenantId);
        }
    }
}
