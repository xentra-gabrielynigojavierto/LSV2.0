using Task.Application.Interfaces;

namespace Task.Application.Services;

/// <summary>
/// TASK-FLOW-03 — default implementation of
/// <see cref="ITaskWorkloadService"/>, thin delegation to repository.
/// </summary>
public sealed class TaskWorkloadService : ITaskWorkloadService
{
    private readonly ITaskWorkloadRepository _repo;

    public TaskWorkloadService(ITaskWorkloadRepository repo) => _repo = repo;

    public System.Threading.Tasks.Task<IReadOnlyList<(string UserId, int Count)>> GetActiveCountsForUsersAsync(
        Guid          tenantId,
        IList<string> userIds,
        CancellationToken ct = default) =>
        _repo.GetActiveCountsForUsersAsync(tenantId, userIds, ct);

    public System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        Guid   tenantId,
        string role,
        int    max,
        CancellationToken ct = default) =>
        _repo.GetUserIdsForRoleAsync(tenantId, role, max, ct);

    public System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        Guid   tenantId,
        string orgId,
        int    max,
        CancellationToken ct = default) =>
        _repo.GetUserIdsForOrgAsync(tenantId, orgId, max, ct);

    public System.Threading.Tasks.Task<bool> HasActiveStepTaskAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string stepKey,
        CancellationToken ct = default) =>
        _repo.HasActiveStepTaskAsync(tenantId, workflowInstanceId, stepKey, ct);
}
