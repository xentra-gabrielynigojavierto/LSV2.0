using Microsoft.Extensions.Options;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Path-scoped authorization middleware for the audit query endpoints.
///
/// Protected path prefix: <c>/audit</c>
/// All other paths (health, swagger, ingest) bypass this middleware entirely.
///
/// Auth flow per request:
/// <list type="number">
///   <item>Skip if path does not start with <c>/audit</c>.</item>
///   <item>Delegate to <see cref="IQueryCallerResolver.ResolveAsync"/> to produce an <see cref="IQueryCallerContext"/>.</item>
///   <item>If Mode ≠ None and caller is not authenticated (scope = Unknown) → 401.</item>
///   <item>Store the caller context in <c>HttpContext.Items[QueryCallerContext.ItemKey]</c>.</item>
///   <item>Call <c>next()</c> — scope enforcement and constraint application happen in the controller via <see cref="IQueryAuthorizer"/>.</item>
/// </list>
///
/// Design rationale:
///   The middleware resolves WHO the caller is. The controller/authorizer determines WHAT they can do.
///   This separation ensures the context is always available to controllers without coupling
///   query-constraint logic to the HTTP pipeline.
///
/// Extension path:
///   To add a new auth mode (e.g. mTLS, API key), implement <see cref="IQueryCallerResolver"/>
///   and add a case to the factory switch in Program.cs. No changes to this middleware needed.
/// </summary>
public sealed class QueryAuthMiddleware
{
    private const string ProtectedPrefix = "/audit";

    private readonly RequestDelegate                   _next;
    private readonly IQueryCallerResolver              _resolver;
    private readonly string                            _mode;
    private readonly ILogger<QueryAuthMiddleware>      _logger;

    public QueryAuthMiddleware(
        RequestDelegate               next,
        IQueryCallerResolver          resolver,
        IOptions<QueryAuthOptions>    opts,
        ILogger<QueryAuthMiddleware>  logger)
    {
        _next     = next;
        _resolver = resolver;
        _mode     = opts.Value.Mode;
        _logger   = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ── Step 1: Path guard ────────────────────────────────────────────────
        var path = context.Request.Path;
        if (!path.StartsWithSegments(ProtectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // ── Step 2: Resolve caller context ────────────────────────────────────
        IQueryCallerContext caller;
        try
        {
            caller = await _resolver.ResolveAsync(context, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "QueryCallerResolver threw an exception. Path={Path} Mode={Mode}",
                path, _mode);
            // Fail safe: return Unknown so the gate below issues a 401.
            caller = QueryCallerContext.Failed(_mode);
        }

        // ── Step 3: Basic auth gate ───────────────────────────────────────────
        // When Mode is not "None", an unauthenticated or unresolved caller gets 401.
        // Fine-grained scope checks (403) are deferred to the controller via IQueryAuthorizer.
        if (!_mode.Equals("None", StringComparison.OrdinalIgnoreCase)
            && caller.Scope == CallerScope.Unknown)
        {
            _logger.LogWarning(
                "Query auth rejected — unauthenticated or unresolvable caller. " +
                "Mode={Mode} Path={Path}",
                _mode, path);

            await WriteJsonErrorAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication is required to access audit records. " +
                "Provide a valid Bearer token.");
            return;
        }

        // ── Step 4: Store context and continue ────────────────────────────────
        context.Items[QueryCallerContext.ItemKey] = caller;

        _logger.LogDebug(
            "Query caller resolved. Scope={Scope} TenantId={TenantId} " +
            "UserId={UserId} IsAuthenticated={Auth} Mode={Mode} Path={Path}",
            caller.Scope, caller.TenantId, caller.UserId,
            caller.IsAuthenticated, _mode, path);

        await _next(context);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
                   ?? System.Diagnostics.Activity.Current?.Id;

        var json = $$"""
            {
              "success": false,
              "message": "{{EscapeJson(message)}}",
              "traceId": {{(traceId is null ? "null" : $"\"{traceId}\"")}},
              "data": null,
              "errors": []
            }
            """;

        await context.Response.WriteAsync(json, context.RequestAborted);
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
