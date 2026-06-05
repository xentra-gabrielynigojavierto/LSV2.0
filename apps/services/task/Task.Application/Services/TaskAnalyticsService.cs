using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Application.Services;

/// <summary>
/// TASK-FLOW-04 — Default implementation of <see cref="ITaskAnalyticsService"/>.
/// Delegates all aggregation to <see cref="ITaskAnalyticsRepository"/>;
/// this layer enforces constant limits and guards before forwarding.
/// </summary>
public sealed class TaskAnalyticsService : ITaskAnalyticsService
{
    private const int OverloadThreshold   = 10;
    private const int MaxQueueBreakdown   = 20;
    private const int MaxTopOverdueQueues = 10;
    private const int MaxTopAssignees     = 20;
    private const int MaxPlatformTenants  = 20;

    private readonly ITaskAnalyticsRepository _repo;

    public TaskAnalyticsService(ITaskAnalyticsRepository repo)
        => _repo = repo;

    public System.Threading.Tasks.Task<TaskSlaAnalyticsResponse> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default)
        => _repo.GetSlaAnalyticsAsync(tenantId, windowStart, windowEnd, MaxTopOverdueQueues, ct);

    public System.Threading.Tasks.Task<TaskQueueAnalyticsResponse> GetQueueAnalyticsAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => _repo.GetQueueAnalyticsAsync(tenantId, OverloadThreshold, MaxQueueBreakdown, ct);

    public System.Threading.Tasks.Task<TaskAssignmentAnalyticsResponse> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default)
        => _repo.GetAssignmentAnalyticsAsync(tenantId, windowStart, windowEnd, MaxTopAssignees, ct);

    public System.Threading.Tasks.Task<TaskPlatformAnalyticsResponse> GetPlatformAnalyticsAsync(
        CancellationToken ct = default)
        => _repo.GetPlatformAnalyticsAsync(MaxPlatformTenants, ct);
}
