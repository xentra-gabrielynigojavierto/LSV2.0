using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Detailed service health and identity endpoint.
///
/// Route: /health/detail
///
/// Distinct from the lightweight liveness probe at /health (mapped via MapHealthChecks)
/// which is intended for orchestrator (k8s) liveness and readiness checks.
/// This endpoint returns rich diagnostic data for monitoring dashboards and human operators.
///
/// Step 21 hardening:
///   - Service name and version are sourced from IOptions&lt;AuditServiceOptions&gt;
///     rather than hardcoded literals so they update automatically when config changes.
///   - Route changed from /health → /health/detail to resolve the routing conflict with
///     the built-in ASP.NET Core health-check endpoint at /health.
/// </summary>
[ApiController]
[Route("health/detail")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly IAuditEventService  _service;
    private readonly AuditServiceOptions _svcOpts;

    public HealthController(
        IAuditEventService          service,
        IOptions<AuditServiceOptions> svcOpts)
    {
        _service = service;
        _svcOpts = svcOpts.Value;
    }

    /// <summary>
    /// Returns service identity metadata and a live event count for diagnostics.
    ///
    /// Note: the EventCount is a live COUNT(*) against the audit record store.
    /// In high-volume deployments, restrict access to this endpoint to internal
    /// callers (network policy / API gateway rule) to avoid both performance
    /// impact and information disclosure to unauthenticated external clients.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var count = await _service.CountAsync(ct);

        var payload = new HealthDetailResponse
        {
            Status      = "Healthy",
            Service     = _svcOpts.ServiceName,
            Version     = _svcOpts.Version,
            Timestamp   = DateTimeOffset.UtcNow,
            EventCount  = count,
        };

        return Ok(ApiResponse<HealthDetailResponse>.Ok(payload, traceId: TraceIdAccessor.Current()));
    }
}

/// <summary>Response shape for the /health/detail endpoint.</summary>
public sealed class HealthDetailResponse
{
    public string         Status     { get; init; } = string.Empty;
    public string         Service    { get; init; } = string.Empty;
    public string         Version    { get; init; } = string.Empty;
    public DateTimeOffset Timestamp  { get; init; }
    public long           EventCount { get; init; }
}
