using Task.Application.DTOs;

namespace Task.Application.Interfaces;

/// <summary>
/// TASK-FLOW-04 — Application-layer analytics service for the Task service.
/// Consumed by the internal Flow analytics endpoints.
/// </summary>
public interface ITaskAnalyticsService
{
    System.Threading.Tasks.Task<TaskSlaAnalyticsResponse> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskQueueAnalyticsResponse> GetQueueAnalyticsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskAssignmentAnalyticsResponse> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskPlatformAnalyticsResponse> GetPlatformAnalyticsAsync(
        CancellationToken ct = default);
}
