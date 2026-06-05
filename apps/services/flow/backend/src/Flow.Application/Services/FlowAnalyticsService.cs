using Flow.Application.DTOs;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// E19 / TASK-FLOW-04 — default implementation of <see cref="IFlowAnalyticsService"/>.
///
/// <para>
/// After TASK-FLOW-04 this service no longer queries <c>flow_workflow_tasks</c>.
/// Task analytics (SLA, queue, assignment) are delegated to the Task service via
/// <see cref="IFlowTaskServiceClient"/>. Flow retains ownership of workflow
/// throughput (WorkflowInstances) and outbox (OutboxMessages) analytics.
/// </para>
///
/// <para>
/// Tenant isolation for task analytics is enforced by passing the tenant ID
/// from <see cref="ITenantProvider"/> to each Task service call.
/// The platform summary endpoint calls Task service with no tenant filter
/// (cross-tenant admin view).
/// </para>
/// </summary>
public sealed class FlowAnalyticsService : IFlowAnalyticsService
{
    private const int OverloadThreshold   = 10;
    private const int MaxQueueBreakdown   = 20;
    private const int MaxTopOverdueQueues = 10;
    private const int MaxTopAssignees     = 20;
    private const int MaxPlatformTenants  = 20;

    private readonly IFlowDbContext           _db;
    private readonly IFlowTaskServiceClient   _taskClient;
    private readonly ITenantProvider          _tenantProvider;
    private readonly ILogger<FlowAnalyticsService> _log;

    public FlowAnalyticsService(
        IFlowDbContext           db,
        IFlowTaskServiceClient   taskClient,
        ITenantProvider          tenantProvider,
        ILogger<FlowAnalyticsService> log)
    {
        _db             = db;
        _taskClient     = taskClient;
        _tenantProvider = tenantProvider;
        _log            = log;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (DateTime start, DateTime end, string label) WindowBounds(AnalyticsWindow window)
    {
        var now = DateTime.UtcNow;
        var start = window switch
        {
            AnalyticsWindow.Today      => now.Date,
            AnalyticsWindow.Last7Days  => now.AddDays(-7),
            AnalyticsWindow.Last30Days => now.AddDays(-30),
            _                          => now.AddDays(-7),
        };
        var label = window switch
        {
            AnalyticsWindow.Today      => "Today",
            AnalyticsWindow.Last7Days  => "Last 7 Days",
            AnalyticsWindow.Last30Days => "Last 30 Days",
            _                          => "Last 7 Days",
        };
        return (start, now, label);
    }

    private Guid CurrentTenantGuid()
    {
        var tid = _tenantProvider.GetTenantId();
        return Guid.TryParse(tid, out var g)
            ? g
            : throw new InvalidOperationException(
                $"FlowAnalyticsService: tenant ID '{tid}' is not a valid Guid.");
    }

    // ── Dashboard Summary ─────────────────────────────────────────────────────

    public async Task<AnalyticsDashboardSummaryDto> GetDashboardSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);

        var (sla, queue, throughput, assignment, outbox) = (
            await GetSlaSummaryAsync(window, ct),
            await GetQueueSummaryAsync(ct),
            await GetWorkflowThroughputAsync(window, ct),
            await GetAssignmentSummaryAsync(window, ct),
            await GetOutboxAnalyticsAsync(window, ct)
        );

        return new AnalyticsDashboardSummaryDto
        {
            Sla         = sla,
            Queue       = queue,
            Workflows   = throughput,
            Assignment  = assignment,
            Outbox      = outbox,
            GeneratedAt = end,
            WindowLabel = label,
        };
    }

    // ── SLA Analytics — Task service ──────────────────────────────────────────

    public async Task<SlaSummaryDto> GetSlaSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var tenantId = CurrentTenantGuid();

        var data = await _taskClient.GetSlaAnalyticsAsync(tenantId, start, end, ct);

        var onTrack = data.SlaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.OnTrack)?.Count ?? 0;
        var atRisk  = data.SlaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.DueSoon)?.Count  ?? 0;
        var overdue = data.SlaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.Overdue)?.Count  ?? 0;
        var total   = onTrack + atRisk + overdue;

        var topOverdueQueues = data.RoleOverdueGroups
            .Select(g => new QueueOverdueBreakdownDto
            {
                QueueKey     = g.QueueKey,
                QueueType    = "Role",
                OverdueCount = g.OverdueCount,
            })
            .Concat(data.OrgOverdueGroups.Select(g => new QueueOverdueBreakdownDto
            {
                QueueKey     = g.QueueKey,
                QueueType    = "Org",
                OverdueCount = g.OverdueCount,
            }))
            .OrderByDescending(x => x.OverdueCount)
            .Take(MaxTopOverdueQueues)
            .ToList();

        return new SlaSummaryDto
        {
            ActiveOnTrackCount      = onTrack,
            ActiveAtRiskCount       = atRisk,
            ActiveOverdueCount      = overdue,
            TotalActiveCount        = total,
            OverduePercentage       = total > 0 ? Math.Round((double)overdue / total * 100, 1) : 0,
            BreachedInWindow        = data.BreachedInWindow,
            CompletedOnTimeInWindow = data.CompletedOnTimeInWindow,
            CompletedInWindow       = data.CompletedInWindow,
            AvgOverdueAgeDays       = data.AvgOverdueAgeDays,
            WindowStart             = start,
            WindowEnd               = end,
            WindowLabel             = label,
            TopOverdueQueues        = topOverdueQueues,
        };
    }

    // ── Queue / Workload Analytics — Task service ──────────────────────────────

    public async Task<QueueSummaryDto> GetQueueSummaryAsync(
        CancellationToken ct = default)
    {
        var now      = DateTime.UtcNow;
        var tenantId = CurrentTenantGuid();

        var data = await _taskClient.GetQueueAnalyticsAsync(tenantId, ct);

        var roleQueueBreakdown = data.RoleGroups
            .GroupBy(x => x.Key)
            .Select(grp => new RoleQueueBacklogDto
            {
                Role            = grp.Key,
                OpenCount       = grp.Where(x => x.Status.Equals("OPEN",        StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status.Equals("IN_PROGRESS", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                TotalCount      = grp.Sum(x => x.Count),
                OverdueCount    = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalCount)
            .Take(MaxQueueBreakdown)
            .ToList();

        var orgQueueBreakdown = data.OrgGroups
            .GroupBy(x => x.Key)
            .Select(grp => new OrgQueueBacklogDto
            {
                OrgId           = grp.Key,
                OpenCount       = grp.Where(x => x.Status.Equals("OPEN",        StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status.Equals("IN_PROGRESS", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                TotalCount      = grp.Sum(x => x.Count),
                OverdueCount    = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalCount)
            .Take(MaxQueueBreakdown)
            .ToList();

        var roleBacklog = roleQueueBreakdown.Sum(x => x.TotalCount);
        var orgBacklog  = orgQueueBreakdown.Sum(x => x.TotalCount);

        return new QueueSummaryDto
        {
            RoleQueueBacklog         = roleBacklog,
            OrgQueueBacklog          = orgBacklog,
            UnassignedBacklog        = data.UnassignedCount,
            OldestQueuedTaskAgeHours = data.OldestQueueAgeHours,
            MedianQueueAgeHours      = data.MedianQueueAgeHours,
            ActiveUserCount          = data.ActiveUserCount,
            OverloadedUserCount      = data.OverloadedUserCount,
            OverloadThreshold        = OverloadThreshold,
            RoleQueueBreakdown       = roleQueueBreakdown,
            OrgQueueBreakdown        = orgQueueBreakdown,
            AsOf                     = now,
        };
    }

    // ── Workflow Throughput — Flow DB only (unchanged) ─────────────────────────

    public async Task<WorkflowThroughputDto> GetWorkflowThroughputAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);

        var started   = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.CreatedAt >= start && i.CreatedAt <= end, ct);

        var completed = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Completed"
                          && i.CompletedAt >= start && i.CompletedAt <= end, ct);

        var cancelled = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Cancelled"
                          && i.UpdatedAt >= start && i.UpdatedAt <= end, ct);

        var failed = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Failed"
                          && i.UpdatedAt >= start && i.UpdatedAt <= end, ct);

        var activeCount = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Active", ct);

        var cycleDates = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.Status == "Completed"
                     && i.CompletedAt >= start && i.CompletedAt <= end
                     && i.CreatedAt != default)
            .Select(i => new { i.CreatedAt, CompletedAt = i.CompletedAt!.Value })
            .ToListAsync(ct);

        double? avgCycleHours    = null;
        double? medianCycleHours = null;
        if (cycleDates.Count > 0)
        {
            var hours = cycleDates
                .Select(x => (x.CompletedAt - x.CreatedAt).TotalHours)
                .Where(h => h >= 0)
                .OrderBy(h => h)
                .ToList();
            if (hours.Count > 0)
            {
                avgCycleHours    = Math.Round(hours.Average(), 2);
                medianCycleHours = Math.Round(hours[hours.Count / 2], 2);
            }
        }

        var productGroups = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.CreatedAt >= start && i.CreatedAt <= end)
            .GroupBy(i => i.ProductKey)
            .Select(g => new WorkflowProductBreakdownDto
            {
                ProductKey     = g.Key,
                StartedCount   = g.Count(),
                CompletedCount = g.Count(x => x.Status == "Completed"),
                ActiveCount    = g.Count(x => x.Status == "Active"),
            })
            .OrderByDescending(x => x.StartedCount)
            .Take(10)
            .ToListAsync(ct);

        return new WorkflowThroughputDto
        {
            StartedInWindow      = started,
            CompletedInWindow    = completed,
            CancelledInWindow    = cancelled,
            FailedInWindow       = failed,
            CurrentlyActiveCount = activeCount,
            AvgCycleTimeHours    = avgCycleHours,
            MedianCycleTimeHours = medianCycleHours,
            ByProduct            = productGroups,
            WindowStart          = start,
            WindowEnd            = end,
            WindowLabel          = label,
        };
    }

    // ── Assignment Analytics — Task service ───────────────────────────────────

    public async Task<AssignmentSummaryDto> GetAssignmentSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var tenantId = CurrentTenantGuid();

        var data = await _taskClient.GetAssignmentAnalyticsAsync(tenantId, start, end, ct);

        var directUser = data.ModeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.DirectUser)?.Count ?? 0;
        var roleQueue  = data.ModeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.RoleQueue)?.Count   ?? 0;
        var orgQueue   = data.ModeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.OrgQueue)?.Count    ?? 0;
        var unassigned = data.ModeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.Unassigned)?.Count  ?? 0;

        var userWorkload = data.UserStatusGroups
            .GroupBy(x => x.UserId)
            .Select(grp => new UserWorkloadDto
            {
                UserId          = grp.Key,
                ActiveTaskCount = grp.Sum(x => x.Count),
                OpenCount       = grp.Where(x => x.Status.Equals("OPEN",        StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status.Equals("IN_PROGRESS", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.ActiveTaskCount)
            .Take(MaxTopAssignees)
            .ToList();

        return new AssignmentSummaryDto
        {
            DirectUserCount          = directUser,
            RoleQueueCount           = roleQueue,
            OrgQueueCount            = orgQueue,
            UnassignedCount          = unassigned,
            AssignedInWindow         = data.AssignedInWindow,
            TopAssigneesByActiveLoad = userWorkload,
            WindowStart              = start,
            WindowEnd                = end,
            WindowLabel              = label,
        };
    }

    // ── Outbox Analytics — Flow DB only (unchanged) ───────────────────────────

    public async Task<OutboxAnalyticsSummaryDto> GetOutboxAnalyticsAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var now = end;

        var statusGroups = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Cnt(string s) => statusGroups.FirstOrDefault(g => g.Status == s)?.Count ?? 0;
        var pending    = Cnt(OutboxStatus.Pending);
        var processing = Cnt(OutboxStatus.Processing);
        var failed     = Cnt(OutboxStatus.Failed);
        var deadLetter = Cnt(OutboxStatus.DeadLettered);
        var succeeded  = Cnt(OutboxStatus.Succeeded);

        var createdInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.CreatedAt >= start && m.CreatedAt <= now, ct);

        var succeededInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.Succeeded
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        var failedInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.Failed
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        var deadLetteredInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.DeadLettered
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        var failedByType = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => new { m.EventType, m.Status })
            .Select(g => new { g.Key.EventType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var byEventType = failedByType
            .GroupBy(x => x.EventType)
            .Select(grp => new OutboxEventTypeBreakdownDto
            {
                EventType      = grp.Key,
                FailedCount    = grp.Where(x => x.Status == OutboxStatus.Failed).Sum(x => x.Count),
                DeadLettered   = grp.Where(x => x.Status == OutboxStatus.DeadLettered).Sum(x => x.Count),
                TotalUnhealthy = grp.Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalUnhealthy)
            .Take(20)
            .ToList();

        return new OutboxAnalyticsSummaryDto
        {
            PendingCount         = pending,
            ProcessingCount      = processing,
            FailedCount          = failed,
            DeadLetteredCount    = deadLetter,
            SucceededCount       = succeeded,
            CreatedInWindow      = createdInWindow,
            SucceededInWindow    = succeededInWindow,
            FailedInWindow       = failedInWindow,
            DeadLetteredInWindow = deadLetteredInWindow,
            FailedByEventType    = byEventType,
            WindowStart          = start,
            WindowEnd            = now,
            WindowLabel          = label,
            AsOf                 = now,
        };
    }

    // ── Platform Summary — Task service + Flow DB ──────────────────────────────

    public async Task<PlatformAnalyticsSummaryDto> GetPlatformSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var now = end;

        // Task service provides cross-tenant task counts and SLA groups.
        var taskData = await _taskClient.GetPlatformAnalyticsAsync(ct);

        // Flow DB provides cross-tenant active workflow count and per-tenant workflow rank.
        var totalActiveWorkflows = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(i => i.Status == "Active", ct);

        // Cross-tenant outbox health (Flow DB)
        var outboxGroups = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalFailed      = outboxGroups.FirstOrDefault(g => g.Status == OutboxStatus.Failed)?.Count      ?? 0;
        var totalDeadLettered = outboxGroups.FirstOrDefault(g => g.Status == OutboxStatus.DeadLettered)?.Count ?? 0;

        // Top tenants by active workflow count (Flow DB)
        var tenantWorkflows = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Status == "Active")
            .GroupBy(i => i.TenantId)
            .Select(g => new TenantWorkflowRankDto { TenantId = g.Key, ActiveCount = g.Count() })
            .OrderByDescending(x => x.ActiveCount)
            .Take(10)
            .ToListAsync(ct);

        // Per-tenant outbox health (Flow DB)
        var tenantOutbox = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => new { m.TenantId, m.Status })
            .Select(g => new { g.Key.TenantId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var outboxByTenant = tenantOutbox
            .GroupBy(x => x.TenantId)
            .Select(grp => new TenantOutboxHealthDto
            {
                TenantId     = grp.Key,
                FailedCount  = grp.Where(x => x.Status == OutboxStatus.Failed).Sum(x => x.Count),
                DeadLettered = grp.Where(x => x.Status == OutboxStatus.DeadLettered).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.FailedCount + x.DeadLettered)
            .Take(MaxPlatformTenants)
            .ToList();

        // Map Task service tenant SLA groups to Flow's TenantOverdueRankDto
        var topByOverdue = taskData.TenantSlaGroups
            .GroupBy(x => x.TenantId)
            .Select(grp =>
            {
                var totalActive  = grp.Sum(x => x.Count);
                var overdueCount = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count);
                return new TenantOverdueRankDto
                {
                    TenantId     = grp.Key.ToString(),
                    OverdueCount = overdueCount,
                    OverdueRate  = totalActive > 0 ? Math.Round((double)overdueCount / totalActive * 100, 1) : 0,
                };
            })
            .OrderByDescending(x => x.OverdueCount)
            .Take(10)
            .ToList();

        return new PlatformAnalyticsSummaryDto
        {
            TotalActiveWorkflows        = totalActiveWorkflows,
            TotalActiveTasks            = taskData.TotalActiveTasks,
            TotalOverdueTasks           = taskData.TotalOverdueTasks,
            TotalDeadLettered           = totalDeadLettered,
            TotalFailedOutbox           = totalFailed,
            TopTenantsByOverdue         = topByOverdue,
            TopTenantsByActiveWorkflows = tenantWorkflows,
            OutboxHealthByTenant        = outboxByTenant,
            AsOf                        = now,
            WindowLabel                 = label,
        };
    }
}
