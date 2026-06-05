using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Services;

namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Path-scoped authentication middleware for the internal audit ingestion endpoints.
///
/// Protected path prefix: <c>/internal/audit</c>
/// All other paths (health, swagger, query API) bypass this middleware entirely.
///
/// Auth flow:
///   1. Skip if path does not start with <c>/internal/audit</c>.
///   2. Skip if Mode = "None" (dev/test pass-through) — still extracts optional headers.
///   3. Delegate to the registered <see cref="IIngestAuthenticator"/> implementation.
///   4. On success: store <see cref="ServiceAuthContext"/> in HttpContext.Items and call next().
///   5. On failure: short-circuit with 401 Unauthorized (missing/invalid token)
///                  or 403 Forbidden (valid token, source not in AllowedSources list).
///
/// Response bodies on failure follow the same <c>ApiResponse</c> envelope used by controllers,
/// ensuring clients get consistent JSON regardless of where the failure originates.
///
/// Extension path:
///   The middleware delegates entirely to <see cref="IIngestAuthenticator"/>.
///   To switch auth mechanisms, swap the registered implementation — no middleware changes needed.
/// </summary>
public sealed class IngestAuthMiddleware
{
    // ── Path prefix protected by this middleware ──────────────────────────────
    private const string ProtectedPrefix = "/internal/audit";

    private readonly RequestDelegate                       _next;
    private readonly IIngestAuthenticator                  _authenticator;
    private readonly IngestAuthOptions                     _options;
    private readonly ILogger<IngestAuthMiddleware>         _logger;

    // Pre-computed AllowedSources set for O(1) lookup.
    private readonly HashSet<string> _allowedSources;

    public IngestAuthMiddleware(
        RequestDelegate                       next,
        IIngestAuthenticator                  authenticator,
        IOptions<IngestAuthOptions>           options,
        ILogger<IngestAuthMiddleware>         logger)
    {
        _next          = next;
        _authenticator = authenticator;
        _options       = options.Value;
        _logger        = logger;

        _allowedSources = _options.AllowedSources is { Count: > 0 }
            ? new HashSet<string>(_options.AllowedSources, StringComparer.OrdinalIgnoreCase)
            : [];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ── Step 1: Path guard ────────────────────────────────────────────────
        // Only protect /internal/audit/* — all other endpoints are unaffected.
        var path = context.Request.Path;
        if (!path.StartsWithSegments(ProtectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // ── Step 2: Mode = None shortcut ──────────────────────────────────────
        // Immediately pass through when auth is explicitly disabled.
        // Still extracts optional context headers so controllers can read them.
        if (_options.Mode.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            SetAnonymousContext(context);
            await _next(context);
            return;
        }

        // ── Step 3: Delegate to authenticator ─────────────────────────────────
        var result = await _authenticator.AuthenticateAsync(context.Request.Headers, context.RequestAborted);

        // ── Step 4: Handle auth failure ───────────────────────────────────────
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Ingest auth rejected — Mode={Mode} Reason={Reason} Path={Path} " +
                "SourceSystem={SourceSystem} SourceService={SourceService}",
                _options.Mode, result.Reason, path,
                result.SourceSystem, result.SourceService);

            await WriteJsonErrorAsync(
                context,
                statusCode: StatusCodes.Status401Unauthorized,
                message:    result.Reason == "MissingToken"
                    ? $"Missing required header: {IngestAuthHeaders.ServiceToken}."
                    : "Authentication failed. Verify your service token is valid and enabled.");
            return;
        }

        // ── Step 5: Source allowlist check ────────────────────────────────────
        // When AllowedSources is configured, the x-source-system header value must
        // match one of the listed identifiers. Empty AllowedSources = allow any.
        if (_allowedSources.Count > 0)
        {
            var inboundSource = result.SourceSystem;
            if (string.IsNullOrWhiteSpace(inboundSource) ||
                !_allowedSources.Contains(inboundSource))
            {
                _logger.LogWarning(
                    "Ingest auth rejected — source not in allowlist. " +
                    "ServiceName={ServiceName} SourceSystem={SourceSystem} AllowedSources={Allowed}",
                    result.ServiceName, inboundSource,
                    string.Join(", ", _allowedSources));

                await WriteJsonErrorAsync(
                    context,
                    statusCode: StatusCodes.Status403Forbidden,
                    message:    string.IsNullOrWhiteSpace(inboundSource)
                        ? $"Header {IngestAuthHeaders.SourceSystem} is required when source allowlist is configured."
                        : $"Source system '{inboundSource}' is not in the configured allowlist.");
                return;
            }
        }

        // ── Step 6: Store auth context and pass through ───────────────────────
        var authContext = new ServiceAuthContext
        {
            ServiceName   = result.ServiceName ?? "unknown",
            SourceSystem  = result.SourceSystem,
            SourceService = result.SourceService,
            AuthMode      = _options.Mode,
        };

        context.Items[ServiceAuthContext.ItemKey] = authContext;

        _logger.LogDebug(
            "Ingest auth accepted — Mode={Mode} ServiceName={ServiceName} " +
            "SourceSystem={SourceSystem} SourceService={SourceService} Path={Path}",
            _options.Mode, authContext.ServiceName,
            authContext.SourceSystem, authContext.SourceService, path);

        await _next(context);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Populates HttpContext.Items with an anonymous <see cref="ServiceAuthContext"/>
    /// when Mode = None (no authentication required).
    /// </summary>
    private void SetAnonymousContext(HttpContext context)
    {
        var headers = context.Request.Headers;

        context.Items[ServiceAuthContext.ItemKey] = new ServiceAuthContext
        {
            ServiceName   = "anonymous",
            SourceSystem  = headers.TryGetValue(IngestAuthHeaders.SourceSystem,  out var ss)   ? ss.ToString()   : null,
            SourceService = headers.TryGetValue(IngestAuthHeaders.SourceService, out var ssvc)  ? ssvc.ToString() : null,
            AuthMode      = "None",
        };
    }

    /// <summary>
    /// Writes a minimal JSON error response in the standard <c>ApiResponse</c> envelope shape.
    /// Does not use the controller ApiResponse type to avoid a reference cycle — writes JSON directly.
    /// </summary>
    private static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
                   ?? System.Diagnostics.Activity.Current?.Id;

        // Matches ApiResponse<T> JSON shape: { success, message, traceId, data, errors }
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
