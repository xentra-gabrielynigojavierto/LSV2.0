namespace Task.Application.Interfaces;

/// <summary>
/// TASK-FLOW-03 — application-layer contract for workload queries
/// delegated from Flow's WorkloadService (E18) and factory dedup.
/// </summary>
public interface ITaskWorkloadService
{
    System.Threading.Tasks.Task<IReadOnlyList<(string UserId, int Count)>> GetActiveCountsForUsersAsync(
        Guid          tenantId,
        IList<string> userIds,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        Guid   tenantId,
        string role,
        int    max,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        Guid   tenantId,
        string orgId,
        int    max,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<bool> HasActiveStepTaskAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string stepKey,
        CancellationToken ct = default);
}
