using Microsoft.EntityFrameworkCore;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Infrastructure.Persistence;

namespace Task.Infrastructure.Persistence.Repositories;

/// <summary>
/// TASK-FLOW-04 — EF Core implementation of <see cref="ITaskAnalyticsRepository"/>.
/// All queries are <c>AsNoTracking</c>. Active statuses are OPEN and IN_PROGRESS.
/// SlaStatus / AssignmentMode values are stored as-is (same string constants
/// used by Flow: OnTrack / DueSoon / Overdue, DirectUser / RoleQueue / OrgQueue / Unassigned).
/// </summary>
public sealed class TaskAnalyticsRepository : ITaskAnalyticsRepository
{
    private static readonly string[] ActiveStatuses = ["OPEN", "IN_PROGRESS"];

    private readonly TasksDbContext _db;

    public TaskAnalyticsRepository(TasksDbContext db) => _db = db;

    // ── SLA Analytics ──────────────────────────────────────────────────────────

    public async System.Threading.Tasks.Task<TaskSlaAnalyticsResponse> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        int       maxOverdueQueues,
        CancellationToken ct = default)
    {
        var q = _db.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

        // 1. Active task SLA breakdown (single GROUP BY)
        var slaGroups = await q
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.SlaStatus)
            .Select(g => new { SlaStatus = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var overdue = slaGroups.FirstOrDefault(g => g.SlaStatus == "Overdue")?.Count ?? 0;

        // 2. Avg overdue age — compute server-side to avoid shipping raw dates over HTTP.
        //    Uses raw SQL AVG on the breach timestamp diff against UTC now.
        double? avgOverdueAgeDays = null;
        if (overdue > 0)
        {
            var breachDates = await q
                .Where(t => ActiveStatuses.Contains(t.Status)
                         && t.SlaStatus == "Overdue"
                         && t.SlaBreachedAt != null)
                .Select(t => t.SlaBreachedAt!.Value)
                .ToListAsync(ct);

            if (breachDates.Count > 0)
                avgOverdueAgeDays = Math.Round(
                    breachDates.Average(d => (windowEnd - d).TotalDays), 2);
        }

        // 3. Window counts
        var breachedInWindow = await q
            .CountAsync(t => t.SlaBreachedAt >= windowStart && t.SlaBreachedAt <= windowEnd, ct);

        var completedInWindow = await q
            .CountAsync(t => t.Status == "COMPLETED"
                          && t.CompletedAt >= windowStart && t.CompletedAt <= windowEnd, ct);

        var completedOnTime = await q
            .CountAsync(t => t.Status == "COMPLETED"
                          && t.CompletedAt >= windowStart && t.CompletedAt <= windowEnd
                          && t.SlaStatus == "OnTrack", ct);

        // 4. Top overdue role queues
        var roleOverdue = await q
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.SlaStatus == "Overdue"
                     && t.AssignmentMode == "RoleQueue"
                     && t.AssignedRole != null)
            .GroupBy(t => t.AssignedRole!)
            .Select(g => new QueueOverdueDto(g.Key, g.Count()))
            .OrderByDescending(x => x.OverdueCount)
            .Take(maxOverdueQueues / 2)
            .ToListAsync(ct);

        // 5. Top overdue org queues
        var orgOverdue = await q
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.SlaStatus == "Overdue"
                     && t.AssignmentMode == "OrgQueue"
                     && t.AssignedOrgId != null)
            .GroupBy(t => t.AssignedOrgId!)
            .Select(g => new QueueOverdueDto(g.Key, g.Count()))
            .OrderByDescending(x => x.OverdueCount)
            .Take(maxOverdueQueues / 2)
            .ToListAsync(ct);

        return new TaskSlaAnalyticsResponse(
            SlaGroups:               slaGroups.Select(g => new SlaStatusCountDto(g.SlaStatus, g.Count)).ToList(),
            AvgOverdueAgeDays:       avgOverdueAgeDays,
            BreachedInWindow:        breachedInWindow,
            CompletedInWindow:       completedInWindow,
            CompletedOnTimeInWindow: completedOnTime,
            RoleOverdueGroups:       roleOverdue,
            OrgOverdueGroups:        orgOverdue);
    }

    // ── Queue Analytics ────────────────────────────────────────────────────────

    public async System.Threading.Tasks.Task<TaskQueueAnalyticsResponse> GetQueueAnalyticsAsync(
        Guid      tenantId,
        int       overloadThreshold,
        int       maxQueueBreakdown,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var q   = _db.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

        // 1. Role queue groups (status × SlaStatus per role)
        var roleGroups = await q
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode == "RoleQueue"
                     && t.AssignedRole != null)
            .GroupBy(t => new { t.AssignedRole, t.Status, t.SlaStatus })
            .Select(g => new QueueGroupCountDto(g.Key.AssignedRole!, g.Key.Status, g.Key.SlaStatus, g.Count()))
            .ToListAsync(ct);

        // Limit to top maxQueueBreakdown roles by total count (in-process)
        var topRoleGroups = roleGroups
            .GroupBy(x => x.Key)
            .OrderByDescending(grp => grp.Sum(x => x.Count))
            .Take(maxQueueBreakdown)
            .SelectMany(grp => grp)
            .ToList();

        // 2. Org queue groups
        var orgGroups = await q
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode == "OrgQueue"
                     && t.AssignedOrgId != null)
            .GroupBy(t => new { t.AssignedOrgId, t.Status, t.SlaStatus })
            .Select(g => new QueueGroupCountDto(g.Key.AssignedOrgId!, g.Key.Status, g.Key.SlaStatus, g.Count()))
            .ToListAsync(ct);

        var topOrgGroups = orgGroups
            .GroupBy(x => x.Key)
            .OrderByDescending(grp => grp.Sum(x => x.Count))
            .Take(maxQueueBreakdown)
            .SelectMany(grp => grp)
            .ToList();

        // 3. Unassigned count
        var unassigned = await q
            .CountAsync(t => ActiveStatuses.Contains(t.Status)
                          && t.AssignmentMode == "Unassigned", ct);

        // 4. Queue age stats (non-DirectUser active tasks)
        var createdAts = await q
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode != "DirectUser")
            .Select(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        double? oldestHours  = null;
        double? medianHours  = null;
        if (createdAts.Count > 0)
        {
            var ages = createdAts.Select(c => (now - c).TotalHours).OrderBy(h => h).ToList();
            oldestHours = Math.Round(ages.Last(), 2);
            medianHours = Math.Round(ages[ages.Count / 2], 2);
        }

        // 5. Per-user workload stats (active assigned tasks)
        var userCounts = await q
            .Where(t => ActiveStatuses.Contains(t.Status) && t.AssignedUserId != null)
            .GroupBy(t => t.AssignedUserId!)
            .Select(g => g.Count())
            .ToListAsync(ct);

        var activeUserCount   = userCounts.Count;
        var overloadedCount   = userCounts.Count(c => c >= overloadThreshold);

        return new TaskQueueAnalyticsResponse(
            RoleGroups:           topRoleGroups,
            OrgGroups:            topOrgGroups,
            UnassignedCount:      unassigned,
            OldestQueueAgeHours:  oldestHours,
            MedianQueueAgeHours:  medianHours,
            ActiveUserCount:      activeUserCount,
            OverloadedUserCount:  overloadedCount);
    }

    // ── Assignment Analytics ───────────────────────────────────────────────────

    public async System.Threading.Tasks.Task<TaskAssignmentAnalyticsResponse> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        int       maxTopAssignees,
        CancellationToken ct = default)
    {
        var q = _db.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

        // 1. All-status mode distribution
        var modeGroups = await q
            .GroupBy(t => t.AssignmentMode)
            .Select(g => new ModeCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        // 2. Assigned in window
        var assignedInWindow = await q
            .CountAsync(t => t.AssignedAt >= windowStart && t.AssignedAt <= windowEnd, ct);

        // 3. Per-user active load (top assignees)
        var userStatusGroups = await q
            .Where(t => ActiveStatuses.Contains(t.Status) && t.AssignedUserId != null)
            .GroupBy(t => new { AssignedUserId = t.AssignedUserId!.ToString(), t.Status })
            .Select(g => new UserStatusCountDto(g.Key.AssignedUserId, g.Key.Status, g.Count()))
            .ToListAsync(ct);

        // Limit to top assignees by total active count (in-process)
        var topUsers = userStatusGroups
            .GroupBy(x => x.UserId)
            .OrderByDescending(grp => grp.Sum(x => x.Count))
            .Take(maxTopAssignees)
            .SelectMany(grp => grp)
            .ToList();

        return new TaskAssignmentAnalyticsResponse(
            ModeGroups:       modeGroups,
            AssignedInWindow: assignedInWindow,
            UserStatusGroups: topUsers);
    }

    // ── Platform Summary (cross-tenant) ────────────────────────────────────────

    public async System.Threading.Tasks.Task<TaskPlatformAnalyticsResponse> GetPlatformAnalyticsAsync(
        int maxTenants,
        CancellationToken ct = default)
    {
        // No tenant filter — cross-tenant admin query.
        var q = _db.Tasks.AsNoTracking();

        var taskStatusSla = await q
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.SlaStatus)
            .Select(g => new { SlaStatus = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalActiveTasks = taskStatusSla.Sum(g => g.Count);
        var totalOverdue     = taskStatusSla.FirstOrDefault(g => g.SlaStatus == "Overdue")?.Count ?? 0;

        // Top tenant SLA breakdown
        var tenantSlaGroups = await q
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => new { t.TenantId, t.SlaStatus })
            .Select(g => new TenantSlaCountDto(g.Key.TenantId, g.Key.SlaStatus, g.Count()))
            .ToListAsync(ct);

        // Limit to top maxTenants by overdue count (in-process)
        var topTenantGroups = tenantSlaGroups
            .GroupBy(x => x.TenantId)
            .OrderByDescending(grp => grp.Where(x => x.SlaStatus == "Overdue").Sum(x => x.Count))
            .Take(maxTenants)
            .SelectMany(grp => grp)
            .ToList();

        return new TaskPlatformAnalyticsResponse(
            TotalActiveTasks:  totalActiveTasks,
            TotalOverdueTasks: totalOverdue,
            TenantSlaGroups:   topTenantGroups);
    }
}
