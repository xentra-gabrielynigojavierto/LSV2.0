using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Alerts;
using PlatformAuditEventService.DTOs.Analytics;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

using AuditEventQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Alert lifecycle endpoints over the audit anomaly detection engine.
///
/// Route prefix: /audit/analytics/alerts
///
/// Endpoints:
///   POST /audit/analytics/alerts/evaluate           — run anomaly detection + upsert alert records
///   GET  /audit/analytics/alerts                    — query alerts with status + tenant filters
///   GET  /audit/analytics/alerts/{id}               — get a single alert by public AlertId
///   POST /audit/analytics/alerts/{id}/acknowledge   — acknowledge an alert
///   POST /audit/analytics/alerts/{id}/resolve       — resolve an alert
///
/// Authorization:
///   Same caller-context / query-authorizer model as AuditAnalyticsController.
///   A probe query validates minimum read access before the service runs.
///   Acknowledge/Resolve additionally identify the operator from the caller context.
///
/// Tenant isolation:
///   Platform admin callers may view cross-tenant alerts or scope to one tenant.
///   Tenant-scoped callers see only their own tenant's alerts.
/// </summary>
[ApiController]
[Route("audit/analytics/alerts")]
[Produces("application/json")]
public sealed class AuditAlertController : ControllerBase
{
    private readonly IAuditAlertService                _alertService;
    private readonly IQueryAuthorizer                  _authorizer;
    private readonly ILogger<AuditAlertController>     _logger;

    public AuditAlertController(
        IAuditAlertService              alertService,
        IQueryAuthorizer                authorizer,
        ILogger<AuditAlertController>   logger)
    {
        _alertService = alertService;
        _authorizer   = authorizer;
        _logger       = logger;
    }

    // ── POST /audit/analytics/alerts/evaluate ─────────────────────────────────

    /// <summary>
    /// Runs the full anomaly detection pipeline and upserts alert records for all firing rules.
    ///
    /// Deduplication: an existing Open/Acknowledged alert for the same condition is refreshed
    /// (LastDetectedAtUtc + DetectionCount incremented) rather than duplicated.
    /// Resolved alerts outside the 1-hour cooldown window produce a new Open alert.
    /// Resolved alerts within the cooldown window are suppressed.
    ///
    /// This endpoint is safe to call repeatedly — deduplication prevents alert storms.
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ApiResponse<AuditEvaluateAlertsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Evaluate(
        [FromQuery] AuditAnomalyRequest request,
        CancellationToken               ct)
    {
        var traceId = TraceIdAccessor.Current();
        var deny    = AuthorizeQuery(new AuditEventQueryRequest(), traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;

        _logger.LogInformation(
            "AuditAlerts/evaluate requested. Caller={Scope} Tenant={Tenant} PA={PA} TraceId={Trace}",
            caller.Scope,
            callerTenantId ?? request.TenantId ?? "(all)",
            isPlatformAdmin,
            traceId);

        var result = await _alertService.EvaluateAsync(request, callerTenantId, isPlatformAdmin, ct);

        return Ok(ApiResponse<AuditEvaluateAlertsResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/analytics/alerts ───────────────────────────────────────────

    /// <summary>
    /// Returns alert records matching the given filter parameters.
    ///
    /// Filter parameters (query string):
    ///   status   — Open | Acknowledged | Resolved (omit for all)
    ///   tenantId — platform admin only; omit for cross-tenant
    ///   limit    — 1-200, default 50
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AuditAlertListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] AuditAlertQueryRequest request,
        CancellationToken                  ct)
    {
        var traceId = TraceIdAccessor.Current();
        var deny    = AuthorizeQuery(new AuditEventQueryRequest(), traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;

        var result = await _alertService.ListAsync(request, callerTenantId, isPlatformAdmin, ct);

        return Ok(ApiResponse<AuditAlertListResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/analytics/alerts/{id} ──────────────────────────────────────

    /// <summary>Returns a single alert by its public AlertId (Guid).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditAlertItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var traceId = TraceIdAccessor.Current();
        var deny    = AuthorizeQuery(new AuditEventQueryRequest(), traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;

        var item = await _alertService.GetByIdAsync(id, callerTenantId, isPlatformAdmin, ct);

        return item is null
            ? NotFound(ApiResponse<object>.Fail($"Alert '{id}' not found.", traceId: traceId))
            : Ok(ApiResponse<AuditAlertItem>.Ok(item, traceId: traceId));
    }

    // ── POST /audit/analytics/alerts/{id}/acknowledge ─────────────────────────

    /// <summary>
    /// Acknowledges an alert. Idempotent — safe to call if already acknowledged.
    /// Sets Status=Acknowledged, AcknowledgedAtUtc=now, AcknowledgedBy=caller identity.
    /// </summary>
    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var traceId = TraceIdAccessor.Current();
        var deny    = AuthorizeQuery(new AuditEventQueryRequest(), traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;
        var callerIdentity  = caller.UserId ?? caller.TenantId ?? "unknown";

        var ok = await _alertService.AcknowledgeAsync(id, callerIdentity, callerTenantId, isPlatformAdmin, ct);

        if (!ok)
            return NotFound(ApiResponse<object>.Fail($"Alert '{id}' not found or not accessible.", traceId: traceId));

        return Ok(ApiResponse<object>.Ok(new { status = "Acknowledged" }, traceId: traceId));
    }

    // ── POST /audit/analytics/alerts/{id}/resolve ─────────────────────────────

    /// <summary>
    /// Resolves an alert. Idempotent — safe to call if already resolved.
    /// Sets Status=Resolved, ResolvedAtUtc=now, ResolvedBy=caller identity.
    /// After resolution, re-detection outside the 1-hour cooldown creates a new alert.
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var traceId = TraceIdAccessor.Current();
        var deny    = AuthorizeQuery(new AuditEventQueryRequest(), traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;
        var callerIdentity  = caller.UserId ?? caller.TenantId ?? "unknown";

        var ok = await _alertService.ResolveAsync(id, callerIdentity, callerTenantId, isPlatformAdmin, ct);

        if (!ok)
            return NotFound(ApiResponse<object>.Fail($"Alert '{id}' not found or not accessible.", traceId: traceId));

        return Ok(ApiResponse<object>.Ok(new { status = "Resolved" }, traceId: traceId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IActionResult? AuthorizeQuery(AuditEventQueryRequest probe, string? traceId)
    {
        var caller = GetCaller();
        var result = _authorizer.Authorize(caller, probe);
        if (result.IsAuthorized) return null;

        _logger.LogWarning(
            "Alert query access denied. Scope={Scope} Status={Status} Reason={Reason} TraceId={TraceId}",
            caller.Scope, result.StatusCode, result.DenialReason, traceId);

        return result.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                Unauthorized(ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
            _ =>
                StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
        };
    }

    private IQueryCallerContext GetCaller() =>
        HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
            && raw is IQueryCallerContext ctx
                ? ctx
                : QueryCallerContext.Anonymous();
}
