using Flow.Application.DTOs;

namespace Flow.Application.Interfaces;

/// <summary>
/// E19 — read-only analytics query service for the LegalSynq Flow platform.
///
/// <para>
/// All methods are tenant-aware: the EF global query filter scopes results to
/// the current tenant unless the caller explicitly passes <c>ignoreTenantFilter = true</c>
/// (reserved for platform admins who call <see cref="GetPlatformSummaryAsync"/>).
/// </para>
///
/// <para>
/// None of these methods write to any table.  Analytics are derived solely
/// from existing operational sources: WorkflowTask, WorkflowInstance,
/// OutboxMessage.
/// </para>
/// </summary>
public interface IFlowAnalyticsService
{
    /// <summary>
    /// Returns a unified dashboard summary covering all five analytics domains
    /// for the current tenant scope.
    /// </summary>
    Task<AnalyticsDashboardSummaryDto> GetDashboardSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);

    /// <summary>
    /// Returns SLA performance metrics for the current tenant.
    /// </summary>
    Task<SlaSummaryDto> GetSlaSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);

    /// <summary>
    /// Returns queue backlog and workload metrics for the current tenant.
    /// </summary>
    Task<QueueSummaryDto> GetQueueSummaryAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Returns workflow throughput metrics for the current tenant.
    /// </summary>
    Task<WorkflowThroughputDto> GetWorkflowThroughputAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);

    /// <summary>
    /// Returns assignment and workload fairness metrics for the current tenant.
    /// </summary>
    Task<AssignmentSummaryDto> GetAssignmentSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);

    /// <summary>
    /// Returns outbox reliability analytics for the current tenant.
    /// Extends the E17 AdminOutboxSummary with window-scoped trend data.
    /// </summary>
    Task<OutboxAnalyticsSummaryDto> GetOutboxAnalyticsAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);

    /// <summary>
    /// Returns cross-tenant platform analytics. Only accessible to platform admins.
    /// Bypasses the EF global tenant filter.
    /// </summary>
    Task<PlatformAnalyticsSummaryDto> GetPlatformSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default);
}
