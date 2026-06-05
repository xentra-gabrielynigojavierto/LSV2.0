using Documents.Application.Exceptions;
using Documents.Infrastructure.Observability;
using System.Text.Json;

namespace Documents.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log  = log;
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
        var correlationId = ctx.GetCorrelationId();

        object body;
        int    statusCode;

        switch (ex)
        {
            // ── Specific DocumentsException subtypes (must come before base) ──

            // 413 — file exceeds upload size limit
            case FileTooLargeException fte:
                statusCode = 413;
                ScanMetrics.UploadFileTooLargeTotal.Inc();
                _log.LogWarning(
                    "Upload rejected — file too large: SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                    fte.FileSizeBytes, fte.LimitMb, correlationId);
                body = new
                {
                    error         = fte.ErrorCode,
                    message       = fte.Message,
                    fileSizeBytes = fte.FileSizeBytes,
                    limitMb       = fte.LimitMb,
                    correlationId,
                };
                break;

            // 422 — file exceeds scanner size limit
            case FileSizeExceedsScanLimitException fse:
                statusCode = 422;
                ScanMetrics.ScanSizeRejectedTotal.Inc();
                _log.LogWarning(
                    "Scan-size rejection: SizeBytes={Size} LimitMb={Limit} CorrelationId={Corr}",
                    fse.FileSizeBytes, fse.LimitMb, correlationId);
                body = new
                {
                    error         = fse.ErrorCode,
                    message       = fse.Message,
                    fileSizeBytes = fse.FileSizeBytes,
                    limitMb       = fse.LimitMb,
                    correlationId,
                };
                break;

            // 503 — scan queue saturated; client must back off and retry
            case QueueSaturationException qse:
                statusCode = 503;
                _log.LogWarning(
                    "Scan queue saturation: upload rejected correlationId={CorrelationId}", correlationId);
                body = new
                {
                    error      = qse.ErrorCode,
                    message    = qse.Message,
                    retryAfter = 30,
                    correlationId,
                };
                ctx.Response.Headers["Retry-After"] = "30";
                break;

            // ── Generic DocumentsException (all remaining subtypes) ───────────

            case DocumentsException de:
                statusCode = de.StatusCode;

                if (statusCode >= 500)
                    _log.LogError(ex, "Internal error [{Code}] correlationId={CorrelationId}", de.ErrorCode, correlationId);
                else
                    _log.LogWarning("Client error [{Code}] {Message} correlationId={CorrelationId}", de.ErrorCode, de.Message, correlationId);

                body = de is ValidationException ve
                    ? (object)new { error = de.ErrorCode, message = de.Message, details = ve.Details, correlationId }
                    : new { error = de.ErrorCode, message = de.Message, correlationId };
                break;

            // ── Auth failure ──────────────────────────────────────────────────

            case UnauthorizedAccessException ue:
                statusCode = 401;
                _log.LogWarning("Authentication failure: {Message}", ue.Message);
                body = new { error = "AUTHENTICATION_REQUIRED", message = "Bearer token required", correlationId };
                break;

            // ── Catch-all ─────────────────────────────────────────────────────

            default:
                statusCode = 500;
                _log.LogError(ex, "Unhandled exception correlationId={CorrelationId}", correlationId);
                body = new { error = "INTERNAL_SERVER_ERROR", message = "An unexpected error occurred", correlationId };
                break;
        }

        ctx.Response.StatusCode  = statusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
