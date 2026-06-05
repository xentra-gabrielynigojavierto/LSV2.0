namespace Task.Application.Interfaces;

/// <summary>
/// TASK-FLOW-03 — repository contract for workload queries used by
/// the Flow service's assignment intelligence (WorkloadService, E18).
///
/// All queries are tenant-scoped and cover active tasks only
/// (status ∈ {OPEN, IN_PROGRESS}).
/// </summary>
public interface ITaskWorkloadRepository
{
    /// <summary>
    /// Returns active task counts grouped by user for the supplied
    /// <paramref name="userIds"/>. Users with zero active tasks are
    /// omitted from the result.
    /// </summary>
    System.Threading.Tasks.Task<IReadOnlyList<(string UserId, int Count)>> GetActiveCountsForUsersAsync(
        Guid         tenantId,
        IList<string> userIds,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct user IDs who have at least one active task
    /// assigned to <paramref name="role"/>, capped at
    /// <paramref name="max"/> results.
    /// </summary>
    System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        Guid   tenantId,
        string role,
        int    max,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct user IDs who have at least one active task
    /// assigned to <paramref name="orgId"/>, capped at
    /// <paramref name="max"/> results.
    /// </summary>
    System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        Guid   tenantId,
        string orgId,
        int    max,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when the tenant already has an active
    /// (Open or InProgress) task for the given
    /// <paramref name="workflowInstanceId"/> + <paramref name="stepKey"/>
    /// pair. Used by Flow's factory dedup check (replaces the shadow
    /// table <c>AnyAsync</c>).
    /// </summary>
    System.Threading.Tasks.Task<bool> HasActiveStepTaskAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string stepKey,
        CancellationToken ct = default);
}
