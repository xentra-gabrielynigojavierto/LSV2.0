using PlatformAuditEventService.DTOs.Analytics;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Evaluates deterministic anomaly detection rules over the canonical audit event store.
///
/// All rules are computed on-demand from two bounded time windows:
/// - Recent window:   [now - 24h, now)
/// - Baseline window: [now - 8d,  now - 1d)  (7 prior calendar days for daily average)
///
/// Tenant isolation mirrors <see cref="IAuditAnalyticsService"/>:
/// <paramref name="callerTenantId"/> non-null → tenant-scoped evaluation.
/// <paramref name="callerTenantId"/> null + <paramref name="isPlatformAdmin"/> true → cross-tenant.
/// </summary>
public interface IAuditAnomalyService
{
    /// <summary>
    /// Evaluates all v1 anomaly rules and returns the list of firing anomalies.
    /// </summary>
    /// <param name="request">Optional tenant scope override (platform admin only).</param>
    /// <param name="callerTenantId">Tenant scope from the authenticated caller. Non-null overrides request.</param>
    /// <param name="isPlatformAdmin">When true, includes cross-tenant rules (TENANT_CONCENTRATION).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditAnomalyResponse> DetectAsync(
        AuditAnomalyRequest request,
        string?             callerTenantId,
        bool                isPlatformAdmin,
        CancellationToken   ct = default);
}
