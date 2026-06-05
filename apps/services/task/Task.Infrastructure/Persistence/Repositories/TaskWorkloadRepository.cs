using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Infrastructure.Persistence;

namespace Task.Infrastructure.Persistence.Repositories;

/// <summary>
/// TASK-FLOW-03 — EF Core implementation of <see cref="ITaskWorkloadRepository"/>.
///
/// All queries are <c>AsNoTracking</c> and tenant-scoped.
/// Active tasks: Status ∈ {"OPEN", "IN_PROGRESS"}.
///
/// <para>
/// Note: <c>PlatformTask.AssignedUserId</c> is stored as <c>Guid?</c>.
/// String user-id inputs are parsed from the caller (Flow's WorkloadService
/// stores user IDs as strings). Unparseable IDs are silently skipped —
/// they can never match a stored Guid row.
/// </para>
/// </summary>
public sealed class TaskWorkloadRepository : ITaskWorkloadRepository
{
    private static readonly string[] ActiveStatuses = ["OPEN", "IN_PROGRESS"];

    private readonly TasksDbContext _db;

    public TaskWorkloadRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<IReadOnlyList<(string UserId, int Count)>> GetActiveCountsForUsersAsync(
        Guid          tenantId,
        IList<string> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return Array.Empty<(string, int)>();

        var guids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();

        if (guids.Count == 0)
            return Array.Empty<(string, int)>();

        var rows = await _db.Tasks
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId
                && t.AssignedUserId != null
                && guids.Contains(t.AssignedUserId.Value)
                && ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.Select(r => (r.UserId.ToString(), r.Count))
                   .ToList()
                   .AsReadOnly();
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        Guid   tenantId,
        string role,
        int    max,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Array.Empty<string>();

        var guids = await _db.Tasks
            .AsNoTracking()
            .Where(t =>
                t.TenantId     == tenantId
                && t.AssignedRole  == role
                && t.AssignedUserId != null
                && ActiveStatuses.Contains(t.Status))
            .Select(t => t.AssignedUserId!.Value)
            .Distinct()
            .Take(max)
            .ToListAsync(ct);

        return guids.Select(g => g.ToString()).ToList().AsReadOnly();
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        Guid   tenantId,
        string orgId,
        int    max,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orgId))
            return Array.Empty<string>();

        var guids = await _db.Tasks
            .AsNoTracking()
            .Where(t =>
                t.TenantId     == tenantId
                && t.AssignedOrgId == orgId
                && t.AssignedUserId != null
                && ActiveStatuses.Contains(t.Status))
            .Select(t => t.AssignedUserId!.Value)
            .Distinct()
            .Take(max)
            .ToListAsync(ct);

        return guids.Select(g => g.ToString()).ToList().AsReadOnly();
    }

    public async System.Threading.Tasks.Task<bool> HasActiveStepTaskAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string stepKey,
        CancellationToken ct = default)
    {
        return await _db.Tasks
            .AsNoTracking()
            .AnyAsync(t =>
                t.TenantId           == tenantId
                && t.WorkflowInstanceId == workflowInstanceId
                && t.WorkflowStepKey    == stepKey
                && ActiveStatuses.Contains(t.Status),
                ct);
    }
}
