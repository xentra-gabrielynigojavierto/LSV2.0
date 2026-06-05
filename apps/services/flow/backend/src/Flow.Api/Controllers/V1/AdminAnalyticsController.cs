using BuildingBlocks.Authorization;
using Flow.Application.DTOs;
using Flow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// E19 — read-only analytics and reporting API for tenant and platform admins.
///
/// <para>
/// Authorization:
///   All domain analytics endpoints require <see cref="Policies.PlatformOrTenantAdmin"/>.
///   The platform summary endpoint (<see cref="GetPlatformSummary"/>) requires
///   <see cref="Policies.PlatformAdmin"/> and returns cross-tenant aggregations.
/// </para>
///
/// <para>
/// Tenant isolation:
///   Tenant admins see only their own tenant's data via the EF global query
///   filter applied by <see cref="IFlowAnalyticsService"/>.
///   Platform admins calling the domain endpoints receive the same tenant-scoped
///   view (their session tenant context); cross-tenant aggregations are exclusively
///   served by <see cref="GetPlatformSummary"/>.
/// </para>
///
/// <para>
/// All endpoints are read-only. No writes are performed by this controller.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/analytics")]
[Authorize(Policy = Policies.PlatformOrTenantAdmin)]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IFlowAnalyticsService _analytics;
    private readonly ILogger<AdminAnalyticsController> _logger;

    public AdminAnalyticsController(
        IFlowAnalyticsService analytics,
        ILogger<AdminAnalyticsController> logger)
    {
        _analytics = analytics;
        _logger    = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AnalyticsWindow ParseWindow(string? window) => window?.ToLowerInvariant() switch
    {
        "today"    => AnalyticsWindow.Today,
        "7d"       => AnalyticsWindow.Last7Days,
        "last7days" => AnalyticsWindow.Last7Days,
        "30d"      => AnalyticsWindow.Last30Days,
        "last30days" => AnalyticsWindow.Last30Days,
        _          => AnalyticsWindow.Last7Days,
    };

    // ── Dashboard Summary ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a unified analytics dashboard summary covering SLA, queue, workflow,
    /// assignment, and outbox analytics for the caller's tenant scope.
    ///
    /// <para>
    /// Query parameter <c>?window=</c> accepts <c>today</c>, <c>7d</c>, or <c>30d</c>.
    /// Default is <c>7d</c>.
    /// </para>
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        var w      = ParseWindow(window);
        var result = await _analytics.GetDashboardSummaryAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Summary window={Window}", w);
        return Ok(result);
    }

    // ── SLA Analytics ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns SLA performance metrics: active task counts by SLA bucket,
    /// breach rates, on-time completion, average overdue age, and top overdue queues.
    /// </summary>
    [HttpGet("sla")]
    public async Task<IActionResult> GetSla(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        var w      = ParseWindow(window);
        var result = await _analytics.GetSlaSummaryAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Sla window={Window}", w);
        return Ok(result);
    }

    // ── Queue / Workload Analytics ────────────────────────────────────────────

    /// <summary>
    /// Returns queue backlog and workload metrics: role-queue and org-queue backlog,
    /// queue age distribution, active tasks per user, overloaded user count,
    /// and per-queue breakdown.
    ///
    /// <para>
    /// Queue analytics are always current-state (no time window). The
    /// <c>?window=</c> parameter is accepted but ignored for queue counts.
    /// </para>
    /// </summary>
    [HttpGet("queues")]
    public async Task<IActionResult> GetQueues(
        CancellationToken ct = default)
    {
        var result = await _analytics.GetQueueSummaryAsync(ct);
        _logger.LogDebug("AdminAnalytics.Queues asOf={AsOf}", result.AsOf);
        return Ok(result);
    }

    // ── Workflow Throughput Analytics ─────────────────────────────────────────

    /// <summary>
    /// Returns workflow throughput metrics: started/completed/cancelled/failed
    /// counts, active workflow count, cycle-time statistics, and per-product breakdown.
    /// </summary>
    [HttpGet("workflows")]
    public async Task<IActionResult> GetWorkflows(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        var w      = ParseWindow(window);
        var result = await _analytics.GetWorkflowThroughputAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Workflows window={Window}", w);
        return Ok(result);
    }

    // ── Assignment / Intelligence Analytics ──────────────────────────────────

    /// <summary>
    /// Returns assignment and workload fairness metrics: assignment mode distribution,
    /// tasks assigned in window, and top assignees by active load.
    ///
    /// <para>
    /// Individual claim / reassign / auto-assign volumes cannot be derived from
    /// WorkflowTask fields alone in E19 and are documented as a known gap.
    /// See <see cref="AssignmentSummaryDto.AssumptionNote"/>.
    /// </para>
    /// </summary>
    [HttpGet("assignment")]
    public async Task<IActionResult> GetAssignment(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        var w      = ParseWindow(window);
        var result = await _analytics.GetAssignmentSummaryAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Assignment window={Window}", w);
        return Ok(result);
    }

    // ── Outbox / Operations Analytics ────────────────────────────────────────

    /// <summary>
    /// Returns outbox reliability analytics: current-state health counts, window-scoped
    /// trends, and failed/dead-lettered breakdown by event type.
    ///
    /// <para>
    /// Extends the E17 <c>GET /api/v1/admin/outbox/summary</c> with window-scoped
    /// trend data and event-type breakdowns. Both endpoints remain available;
    /// this endpoint is additive.
    /// </para>
    /// </summary>
    [HttpGet("outbox")]
    public async Task<IActionResult> GetOutbox(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        var w      = ParseWindow(window);
        var result = await _analytics.GetOutboxAnalyticsAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Outbox window={Window}", w);
        return Ok(result);
    }

    // ── Platform Summary (cross-tenant) ───────────────────────────────────────

    /// <summary>
    /// Returns cross-tenant platform analytics: total active workflows/tasks,
    /// overdue totals, dead-letter counts, top tenants by overdue rate and
    /// active workflow count, and outbox health by tenant.
    ///
    /// <para>
    /// Restricted to platform admins. Tenant admins receive 403.
    /// </para>
    /// </summary>
    [HttpGet("platform")]
    public async Task<IActionResult> GetPlatformSummary(
        [FromQuery] string?           window = null,
        CancellationToken             ct     = default)
    {
        if (!User.IsInRole(Roles.PlatformAdmin))
            return Forbid();

        var w      = ParseWindow(window);
        var result = await _analytics.GetPlatformSummaryAsync(w, ct);
        _logger.LogDebug("AdminAnalytics.Platform window={Window}", w);
        return Ok(result);
    }
}
