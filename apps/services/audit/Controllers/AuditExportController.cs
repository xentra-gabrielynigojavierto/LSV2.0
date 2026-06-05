using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Export;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Enums;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

// Disambiguate internal ingest DTO from external client DTO aliases.
using IngestRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Export lifecycle endpoints.
///
/// Route prefix: /audit
///
///   POST /audit/exports                 — Submit an async export job.
///   GET  /audit/exports/{exportId}      — Poll the status of an existing job.
///
/// Both endpoints sit under the /audit/* path prefix and are subject to
/// QueryAuthMiddleware scope resolution. Export creation enforces the same
/// multi-tenancy constraints as query endpoints via IQueryAuthorizer.
///
/// Configuration gate:
///   When Export:Provider = "None" (the default), both endpoints return HTTP 503.
///   Set Export:Provider = "Local" (dev) or "S3" / "AzureBlob" (production) to enable.
///
/// Response codes:
///   POST: 202 Accepted  — job created and processed; body = ApiResponse&lt;ExportStatusResponse&gt;.
///         400 Bad Request — validation failure or unsupported format.
///         401 / 403       — authentication / scope failure.
///         503 Service Unavailable — export not configured.
///
///   GET:  200 OK        — job found; body = ApiResponse&lt;ExportStatusResponse&gt;.
///         404 Not Found — no job with the given exportId.
///         503 Service Unavailable — export not configured.
///
/// Step 21 hardening:
///   All error paths now return the ApiResponse&lt;T&gt; envelope for client-contract
///   consistency. Raw anonymous objects have been replaced throughout.
///
/// LS-ID-TNT-017-002-01:
///   Successful export submission emits <c>audit.log.exported</c> via the canonical
///   ingestion pipeline (fire-and-observe, same pattern as AuditEventQueryController).
/// </summary>
[ApiController]
[Route("audit")]
[Produces("application/json")]
public sealed class AuditExportController : ControllerBase
{
    private readonly IAuditExportService                _exportService;
    private readonly IAuditEventIngestionService        _ingestionService;
    private readonly IValidator<ExportRequest>          _validator;
    private readonly ExportOptions                      _exportOpts;
    private readonly ILogger<AuditExportController>     _logger;

    public AuditExportController(
        IAuditExportService             exportService,
        IAuditEventIngestionService     ingestionService,
        IValidator<ExportRequest>       validator,
        IOptions<ExportOptions>         exportOpts,
        ILogger<AuditExportController>  logger)
    {
        _exportService    = exportService;
        _ingestionService = ingestionService;
        _validator        = validator;
        _exportOpts       = exportOpts.Value;
        _logger           = logger;
    }

    // ── POST /audit/exports ───────────────────────────────────────────────────

    /// <summary>
    /// Submit a new audit data export job.
    ///
    /// Accepts filter parameters similar to the query API. Creates a job, processes it
    /// synchronously (v1), and returns the terminal status with the output file reference.
    ///
    /// The caller's authorization scope (resolved by QueryAuthMiddleware) determines
    /// which records are accessible. Cross-tenant access is always denied for
    /// non-PlatformAdmin callers.
    /// </summary>
    [HttpPost("exports")]
    [ProducesResponseType(typeof(ApiResponse<ExportStatusResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Submit(
        [FromBody] ExportRequest request,
        CancellationToken        ct)
    {
        var traceId = TraceIdAccessor.Current();

        if (!IsExportEnabled(out var disabledResult))
            return disabledResult!;

        // ── Validation ────────────────────────────────────────────────────────
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.ValidationFail(
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                traceId: traceId));
        }

        // Instance-level format gate (in addition to static validator list)
        if (!_exportOpts.SupportedFormats.Contains(request.Format, StringComparer.Ordinal))
        {
            return BadRequest(ApiResponse<object>.Fail(
                $"Format '{request.Format}' is not enabled on this instance. " +
                $"Supported: {string.Join(", ", _exportOpts.SupportedFormats)}.",
                traceId: traceId));
        }

        // ── Caller context (set by QueryAuthMiddleware) ────────────────────────
        var caller = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext
                     ?? QueryCallerContext.Anonymous();

        // ── Delegate to service ───────────────────────────────────────────────
        try
        {
            var result = await _exportService.SubmitAsync(request, caller, ct);

            // ── Canonical audit: audit.log.exported ───────────────────────────
            // LS-ID-TNT-017-002-01: Emit a governance access event capturing who
            // initiated this audit log data export. Fire-and-observe — the 202 response
            // is never gated on audit publish success.
            LogAuditExport(caller, result, traceId);

            return StatusCode(StatusCodes.Status202Accepted,
                ApiResponse<ExportStatusResponse>.Ok(result, traceId: traceId));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                "Export access denied for ExportRequest. Scope={Scope} TraceId={TraceId}",
                caller.Scope, traceId);

            // Do not forward ex.Message to the client — log it only.
            _ = ex;

            return caller.IsAuthenticated
                ? StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail("Access denied.", traceId: traceId))
                : StatusCode(StatusCodes.Status401Unauthorized,
                    ApiResponse<object>.Fail("Authentication is required to submit an export.", traceId: traceId));
        }
    }

    // ── GET /audit/exports/{exportId} ─────────────────────────────────────────

    /// <summary>
    /// Poll the status of an existing export job.
    ///
    /// Returns the current state of the job including file path / download URL
    /// when Status = Completed, or ErrorMessage when Status = Failed.
    ///
    /// Polling is idempotent — repeated calls return the same data once the job
    /// reaches a terminal state (Completed, Failed, Cancelled, Expired).
    /// </summary>
    [HttpGet("exports/{exportId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ExportStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>),               StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetStatus(
        Guid              exportId,
        CancellationToken ct)
    {
        var traceId = TraceIdAccessor.Current();

        if (!IsExportEnabled(out var disabledResult))
            return disabledResult!;

        // Resolve the caller context so ownership can be verified by the service.
        // GetStatusAsync returns null for jobs that don't belong to this caller
        // (unless the caller is PlatformAdmin), preventing cross-tenant job enumeration.
        var caller = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext
                     ?? QueryCallerContext.Anonymous();

        var result = await _exportService.GetStatusAsync(exportId, caller, ct);

        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                $"Export job '{exportId}' not found.",
                traceId: traceId));
        }

        return Ok(ApiResponse<ExportStatusResponse>.Ok(result, traceId: traceId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns false and sets <paramref name="result"/> to a 503 ApiResponse when
    /// Export:Provider = "None". Returns true when the export subsystem is active.
    /// </summary>
    private bool IsExportEnabled(out IActionResult? result)
    {
        if (_exportOpts.Provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            var traceId = TraceIdAccessor.Current();
            result = StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    "The export service is not enabled on this instance. " +
                    "Set Export:Provider to 'Local', 'S3', or 'AzureBlob' to activate.",
                    traceId: traceId));
            return false;
        }

        result = null;
        return true;
    }

    /// <summary>
    /// Emits a <c>audit.log.exported</c> canonical audit event when an audit log
    /// export job is successfully submitted.
    ///
    /// LS-ID-TNT-017-002-01: Selective Successful Access Audit.
    ///
    /// Fire-and-observe: the returned Task is discarded. This method never throws,
    /// and the 202 response is always returned regardless of audit publish outcome.
    ///
    /// Mirrors the <c>LogAuditAccess</c> pattern from <see cref="AuditEventQueryController"/>:
    ///   - same ingestion service
    ///   - same EventCategory.Access
    ///   - same VisibilityScope.Platform
    ///   - same tenant isolation via caller.TenantId
    /// </summary>
    private void LogAuditExport(
        IQueryCallerContext  caller,
        ExportStatusResponse result,
        string?              traceId)
    {
        _logger.LogInformation(
            "AuditExport: ExportId={ExportId} Scope={Scope} AuthMode={AuthMode} Format={Format} RecordCount={RecordCount} TraceId={TraceId}",
            result.ExportId,
            caller.Scope,
            caller.AuthMode,
            result.Format,
            result.RecordCount ?? 0,
            traceId ?? "(no-trace)");

        var now = DateTimeOffset.UtcNow;
        _ = _ingestionService.IngestSingleAsync(new IngestRequest
        {
            EventType       = "audit.log.exported",
            EventCategory   = EventCategory.Access,
            SourceSystem    = "audit",
            SourceService   = "audit-export-api",
            Visibility      = VisibilityScope.Platform,
            Severity        = SeverityLevel.Warn,
            OccurredAtUtc   = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = caller.TenantId,
            },
            Actor = new AuditEventActorDto
            {
                Id   = caller.UserId,
                Type = ActorType.User,
                Name = caller.UserId ?? "(anonymous)",
            },
            Entity = new AuditEventEntityDto
            {
                Type = "AuditExport",
                Id   = result.ExportId.ToString(),
            },
            Action      = "ExportSubmitted",
            Description = $"Audit log export submitted — {result.RecordCount ?? 0} record(s), format={result.Format}.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                endpoint       = "POST /audit/exports",
                exportId       = result.ExportId,
                format         = result.Format,
                recordCount    = result.RecordCount ?? 0,
                callerScope    = caller.Scope.ToString(),
                callerAuthMode = caller.AuthMode,
                traceId        = traceId ?? "(none)",
            }),
            CorrelationId  = traceId,
            IdempotencyKey = $"audit-export:{result.ExportId}",
            Tags           = ["audit-of-audit", "export", "data-egress"],
        });
    }
}
