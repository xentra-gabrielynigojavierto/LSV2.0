using System.Text;
using System.Text.Json;
using BuildingBlocks.Context;

namespace Flow.Api.Middleware;

/// <summary>
/// Defense-in-depth: rejects requests where a client-supplied tenant identifier
/// (query string or JSON body) disagrees with the authenticated tenant on the
/// JWT. Mirrors the platform pattern established in Reports/Liens.
/// </summary>
public sealed class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantValidationMiddleware> _log;

    private static readonly HashSet<string> MutationMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    public TenantValidationMiddleware(RequestDelegate next, ILogger<TenantValidationMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExcludedPath(context))
        {
            await _next(context);
            return;
        }

        var requestContext = context.RequestServices.GetService<ICurrentRequestContext>();
        if (requestContext is null || !requestContext.IsAuthenticated || requestContext.TenantId is null)
        {
            await _next(context);
            return;
        }

        var claimTenantId = requestContext.TenantId.Value.ToString("D").ToLowerInvariant();

        if (context.Request.Query.TryGetValue("tenantId", out var queryTenantId) &&
            !string.IsNullOrWhiteSpace(queryTenantId))
        {
            if (!string.Equals(queryTenantId.ToString(), claimTenantId, StringComparison.OrdinalIgnoreCase))
            {
                await WriteForbidden(context, queryTenantId.ToString()!, claimTenantId, "query");
                return;
            }
        }

        if (MutationMethods.Contains(context.Request.Method) &&
            context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Request.EnableBuffering();
            var body = await ReadBodyAsync(context.Request);
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                string? bodyTenantId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("tenantId", out var tid) ||
                        doc.RootElement.TryGetProperty("TenantId", out tid))
                    {
                        bodyTenantId = tid.GetString();
                    }
                }
                catch (JsonException) { }

                if (bodyTenantId is not null &&
                    !string.Equals(bodyTenantId, claimTenantId, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteForbidden(context, bodyTenantId, claimTenantId, "body");
                    return;
                }
            }
        }

        await _next(context);
    }

    private async Task WriteForbidden(HttpContext context, string supplied, string claim, string source)
    {
        _log.LogWarning(
            "Tenant isolation violation: {Source} tenantId={Supplied} != JWT tenant_id={Claim} on {Method} {Path}",
            source, supplied, claim, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                error = "Tenant isolation violation: request tenant does not match authenticated tenant."
            }));
    }

    private static bool IsExcludedPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/api/v1/status", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
