using Task.Application.DTOs;

namespace Task.Application.Interfaces;

/// <summary>
/// TASK-FLOW-04 — Analytics query interface executed against the Task DB.
/// All aggregate computations happen here so callers receive ready-to-use
/// response DTOs rather than raw data that would require further processing.
/// Methods are named after the Flow analytics surface they support, keeping
/// the Task service generic (it stores the same fields for any consumer).
/// </summary>
public interface ITaskAnalyticsRepository
{
    // ── Tenant-scoped queries ──────────────────────────────────────────────────

    System.Threading.Tasks.Task<TaskSlaAnalyticsResponse> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        int       maxOverdueQueues,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskQueueAnalyticsResponse> GetQueueAnalyticsAsync(
        Guid      tenantId,
        int       overloadThreshold,
        int       maxQueueBreakdown,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskAssignmentAnalyticsResponse> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        int       maxTopAssignees,
        CancellationToken ct = default);

    // ── Cross-tenant (platform admin) ─────────────────────────────────────────

    System.Threading.Tasks.Task<TaskPlatformAnalyticsResponse> GetPlatformAnalyticsAsync(
        int maxTenants,
        CancellationToken ct = default);
}
