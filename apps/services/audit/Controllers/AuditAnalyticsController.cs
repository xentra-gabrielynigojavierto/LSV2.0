using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Analytics;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

using AuditEventQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Analytics and anomaly detection endpoints over the canonical audit event store.
///
/// Route prefix: /audit/analytics
///
/// Endpoints:
///   GET /audit/analytics/summary   — aggregated analytics for a configurable date window
///   GET /audit/analytics/anomalies — deterministic rule-based anomaly detection (fixed 24h/7d windows)
///
/// Authorization: same caller-context / query-authorizer model as
/// <see cref="AuditEventQueryController"/>. A probe query is used to validate
/// that the caller has at least basic read access before any service runs.
///
/// Tenant isolation:
/// - Platform admin callers (CallerScope.PlatformAdmin) may omit tenantId for
///   cross-tenant views, or supply tenantId to scope to a single tenant.
/// - All other callers see only their own tenant's data regardless of request parameters.
/// </summary>
[ApiController]
[Route("audit/analytics")]
[Produces("application/json")]
public sealed class AuditAnalyticsController : ControllerBase
{
    private readonly IAuditAnalyticsService                _analyticsService;
    private readonly IAuditAnomalyService                  _anomalyService;
    private readonly IQueryAuthorizer                      _authorizer;
    private readonly ILogger<AuditAnalyticsController>     _logger;

    public AuditAnalyticsController(
        IAuditAnalyticsService             analyticsService,
        IAuditAnomalyService               anomalyService,
        IQueryAuthorizer                   authorizer,
        ILogger<AuditAnalyticsController>  logger)
    {
        _analyticsService = analyticsService;
        _anomalyService   = anomalyService;
        _authorizer       = authorizer;
        _logger           = logger;
    }

    // ── GET /audit/analytics/summary ──────────────────────────────────────────

    /// <summary>
    /// Returns a comprehensive audit analytics summary for the specified window.
    ///
    /// Sub-sections included in the response:
    /// - KPI scalars: totalEvents, securityEventCount, denialEventCount, governanceEventCount
    /// - VolumeByDay: per-calendar-day event count (chronological, UTC dates)
    /// - ByCategory: count per EventCategory, descending
    /// - BySeverity: count per SeverityLevel, ascending
    /// - TopEventTypes: top 15 event types by count
    /// - TopActors: top 10 actors by count
    /// - TopTenants: top 10 tenants by count — only for platform admin callers
    ///
    /// Filter parameters:
    /// - from / to: date window (default: last 30 days, max: 90 days)
    /// - tenantId: platform admin only — omit for cross-tenant
    /// - category: optional category filter applied to all sub-queries
    /// </summary>
    /// <param name="request">Analytics filter parameters (bound from query string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Analytics summary computed successfully.</response>
    /// <response code="400">Invalid filter parameters (e.g. To before From).</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller does not have read access to the audit log.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<AuditAnalyticsSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] AuditAnalyticsSummaryRequest request,
        CancellationToken                        ct)
    {
        var traceId = TraceIdAccessor.Current();

        var probeQuery = new AuditEventQueryRequest();
        var deny       = AuthorizeQuery(probeQuery, traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;

        if (request.From.HasValue && request.To.HasValue && request.From >= request.To)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "'from' must be earlier than 'to'.", traceId: traceId));
        }

        _logger.LogInformation(
            "AuditAnalytics/summary requested. Caller={Scope} TenantId={Tenant} " +
            "PA={PA} From={From} To={To} TraceId={Trace}",
            caller.Scope,
            callerTenantId ?? request.TenantId ?? "(all)",
            isPlatformAdmin,
            request.From?.ToString("u") ?? "(default)",
            request.To?.ToString("u")   ?? "(now)",
            traceId);

        var result = await _analyticsService.GetSummaryAsync(
            request,
            callerTenantId,
            isPlatformAdmin,
            ct);

        return Ok(ApiResponse<AuditAnalyticsSummaryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/analytics/anomalies ────────────────────────────────────────

    /// <summary>
    /// Evaluates deterministic anomaly detection rules over the last 24 hours
    /// compared to a 7-day baseline window.
    ///
    /// Rules evaluated:
    /// - DENIAL_SPIKE           — denial events 3× the 7-day daily average (≥5 events)
    /// - ACTOR_CONCENTRATION    — one actor ≥30% of all events (≥20 events)
    /// - TENANT_CONCENTRATION   — one tenant ≥40% of platform events (≥50 events; PA only)
    /// - GOVERNANCE_BURST       — governance events 3× the 7-day daily average (≥3 events)
    /// - EXPORT_SPIKE           — audit access/export events 3× daily average (≥5 events)
    /// - SEVERITY_ESCALATION    — Critical/Alert events >10 absolute OR ≥10% of total
    /// - EVENTTYPE_CONCENTRATION — one event type ≥50% of all events (≥30 events)
    ///
    /// An empty anomalies list is a valid response (no anomalies detected).
    /// </summary>
    /// <param name="request">Optional tenant scope (platform admin only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Anomaly evaluation complete. Body contains firing anomalies (may be empty).</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller does not have read access to the audit log.</response>
    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(ApiResponse<AuditAnomalyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAnomalies(
        [FromQuery] AuditAnomalyRequest request,
        CancellationToken               ct)
    {
        var traceId = TraceIdAccessor.Current();

        var probeQuery = new AuditEventQueryRequest();
        var deny       = AuthorizeQuery(probeQuery, traceId);
        if (deny is not null) return deny;

        var caller          = GetCaller();
        var isPlatformAdmin = caller.Scope == CallerScope.PlatformAdmin;
        var callerTenantId  = isPlatformAdmin ? null : caller.TenantId;

        _logger.LogInformation(
            "AuditAnalytics/anomalies requested. Caller={Scope} TenantId={Tenant} PA={PA} TraceId={Trace}",
            caller.Scope,
            callerTenantId ?? request.TenantId ?? "(all)",
            isPlatformAdmin,
            traceId);

        var result = await _anomalyService.DetectAsync(
            request,
            callerTenantId,
            isPlatformAdmin,
            ct);

        return Ok(ApiResponse<AuditAnomalyResponse>.Ok(result, traceId: traceId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the caller from HttpContext.Items and runs the authorization probe.
    /// Returns a denial IActionResult when the caller is not authorized; null on success.
    /// </summary>
    private IActionResult? AuthorizeQuery(AuditEventQueryRequest probe, string? traceId)
    {
        var caller = GetCaller();
        var result = _authorizer.Authorize(caller, probe);

        if (result.IsAuthorized) return null;

        _logger.LogWarning(
            "Analytics query access denied. Scope={Scope} Status={Status} " +
            "Reason={Reason} TraceId={TraceId}",
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

    /// <summary>
    /// Returns the caller context for this request.
    /// Reads from HttpContext.Items (set by QueryAuthMiddleware).
    /// Falls back to Anonymous when middleware is bypassed.
    /// </summary>
    private IQueryCallerContext GetCaller() =>
        HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
            && raw is IQueryCallerContext ctx
                ? ctx
                : QueryCallerContext.Anonymous();
}
