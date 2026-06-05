using PlatformAuditEventService.DTOs.Analytics;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Produces aggregated audit analytics over the canonical AuditEventRecord store.
///
/// All methods enforce tenant isolation: if <paramref name="callerTenantId"/> is set,
/// it overrides any tenant requested by the caller. Platform admins pass null to
/// allow cross-tenant queries.
/// </summary>
public interface IAuditAnalyticsService
{
    /// <summary>
    /// Returns the full analytics summary for the given window and scope.
    /// </summary>
    /// <param name="request">Filter parameters (From, To, optional TenantId/Category).</param>
    /// <param name="callerTenantId">
    ///   Tenant scope from the authenticated caller.
    ///   Non-null ⇒ tenant-scoped view (overrides request.TenantId).
    ///   Null ⇒ platform admin; uses request.TenantId or cross-tenant.
    /// </param>
    /// <param name="isPlatformAdmin">When true, includes TopTenants in response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditAnalyticsSummaryResponse> GetSummaryAsync(
        AuditAnalyticsSummaryRequest request,
        string?                      callerTenantId,
        bool                         isPlatformAdmin,
        CancellationToken            ct = default);
}
