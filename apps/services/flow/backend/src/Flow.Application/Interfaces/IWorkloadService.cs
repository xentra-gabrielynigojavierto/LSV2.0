namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E18 — read-only workload-tracking surface for the
/// work-distribution intelligence layer.
///
/// <para>
/// Provides active task counts per user, scoped to the caller's tenant
/// (via the global EF query filter on <c>WorkflowTask</c>). The counts
/// drive the capacity model used by
/// <see cref="ITaskRecommendationService"/> to rank and explain
/// assignee recommendations.
/// </para>
///
/// <para>
/// <b>Active workload definition:</b> a task contributes to a user's
/// workload count when:
///   <list type="bullet">
///     <item><c>Status</c> is <c>Open</c> or <c>InProgress</c>.</item>
///     <item><c>AssignedUserId</c> is non-null (queue rows are
///       excluded — they have not yet been claimed by a user).</item>
///   </list>
/// Terminal tasks (<c>Completed</c>, <c>Cancelled</c>) are excluded.
/// </para>
/// </summary>
public interface IWorkloadService
{
    /// <summary>
    /// Returns the active task count for each of the supplied user ids
    /// within the caller's tenant. Users with no active tasks are
    /// omitted from the result; callers should treat missing keys as 0.
    /// </summary>
    /// <param name="userIds">
    /// The set of user ids to query. An empty set returns an empty
    /// dictionary immediately without a DB round-trip.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Dictionary mapping user id → active task count (≥ 1).
    /// </returns>
    Task<IReadOnlyDictionary<string, int>> GetActiveTaskCountsAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct user ids that have at least one active task
    /// associated with the given <paramref name="assignedRole"/> (i.e.,
    /// tasks where <c>AssignedRole = role</c> and
    /// <c>AssignedUserId IS NOT NULL</c>), within the caller's tenant.
    ///
    /// <para>
    /// Used by the recommendation engine to derive a candidate list for
    /// RoleQueue tasks when the caller does not supply one explicitly.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        string assignedRole,
        int maxResults,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct user ids that have at least one active task
    /// associated with the given <paramref name="assignedOrgId"/>,
    /// within the caller's tenant.
    ///
    /// <para>
    /// Used by the recommendation engine to derive a candidate list for
    /// OrgQueue tasks when the caller does not supply one explicitly.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        string assignedOrgId,
        int maxResults,
        CancellationToken ct = default);
}
