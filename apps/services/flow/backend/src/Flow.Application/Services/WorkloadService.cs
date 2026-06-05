using Flow.Application.Interfaces;
using Flow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E18 / TASK-FLOW-03 — default implementation of
/// <see cref="IWorkloadService"/>.
///
/// <para>
/// After TASK-FLOW-03 this service no longer queries
/// <c>flow_workflow_tasks</c>. All workload queries are delegated to
/// the Task service via <see cref="IFlowTaskServiceClient"/>, which
/// aggregates directly against the canonical <c>tasks</c> table.
/// </para>
///
/// <para>
/// Tenant isolation is enforced by passing the tenant ID from
/// <see cref="ITenantProvider"/> to every Task service call.
/// </para>
/// </summary>
public sealed class WorkloadService : IWorkloadService
{
    private readonly IFlowTaskServiceClient   _taskClient;
    private readonly ITenantProvider          _tenantProvider;
    private readonly ILogger<WorkloadService> _log;

    public WorkloadService(
        IFlowTaskServiceClient   taskClient,
        ITenantProvider          tenantProvider,
        ILogger<WorkloadService> log)
    {
        _taskClient     = taskClient;
        _tenantProvider = tenantProvider;
        _log            = log;
    }

    private Guid CurrentTenantGuid()
    {
        var tid = _tenantProvider.GetTenantId();
        return Guid.TryParse(tid, out var g)
            ? g
            : throw new InvalidOperationException(
                $"WorkloadService: tenant ID '{tid}' is not a valid Guid.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetActiveTaskCountsAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var tenantId = CurrentTenantGuid();
        var result   = await _taskClient.GetWorkloadCountsAsync(tenantId, ids, ct);

        _log.LogDebug(
            "WorkloadService.GetActiveTaskCountsAsync: queried {Requested} users, found workload for {WithLoad} users",
            ids.Count, result.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        string assignedRole,
        int    maxResults,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assignedRole))
            return Array.Empty<string>();

        var tenantId = CurrentTenantGuid();
        var ids = await _taskClient.GetWorkloadUsersByRoleAsync(tenantId, assignedRole, maxResults, ct);

        _log.LogDebug(
            "WorkloadService.GetUserIdsForRoleAsync: role={Role} → {Count} candidate(s)",
            assignedRole, ids.Count);

        return ids;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        string assignedOrgId,
        int    maxResults,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assignedOrgId))
            return Array.Empty<string>();

        var tenantId = CurrentTenantGuid();
        var ids = await _taskClient.GetWorkloadUsersByOrgAsync(tenantId, assignedOrgId, maxResults, ct);

        _log.LogDebug(
            "WorkloadService.GetUserIdsForOrgAsync: orgId={OrgId} → {Count} candidate(s)",
            assignedOrgId, ids.Count);

        return ids;
    }
}
