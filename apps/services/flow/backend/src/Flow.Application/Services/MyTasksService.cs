using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// TASK-FLOW-02 — read paths migrated from <c>flow_workflow_tasks</c> to the
/// canonical Task service.
///
/// <para>
/// All four surfaces — My Tasks, Role Queue, Org Queue, and Task Detail — now
/// call <see cref="IFlowTaskServiceClient"/> for the task rows, then enrich
/// the results with <c>WorkflowName</c> / <c>ProductKey</c> by batch-querying
/// <see cref="IFlowDbContext.WorkflowInstances"/> on the Flow DB.
/// </para>
///
/// <para>
/// <b>Status string contract</b>: the Task service uses uppercase snake-case
/// status strings (OPEN, IN_PROGRESS, COMPLETED, CANCELLED). The helper
/// <see cref="ToTaskServiceStatus"/> converts the caller's Flow-convention
/// strings to the Task service format; <see cref="MapStatus"/> converts back.
/// </para>
///
/// <para>
/// <b>Role-queue pagination</b>: for non-admin callers with multiple roles,
/// the Task service does not yet accept a multi-role filter. We loop once per
/// role (capped at <see cref="RoleQueuePerRoleFetchCap"/> items), merge, dedupe
/// by TaskId, sort in-memory with the same urgency hierarchy as before, then
/// slice the requested page. This is correct for the current workloads; a
/// multi-role Task service filter will be added in a later sprint.
/// </para>
///
/// <para>
/// <b>Tenant safety</b>: the Task service enforces tenant isolation via JWT
/// claims (the forwarded bearer token). We do not apply extra tenant filters
/// beyond what the user's token already provides.
/// </para>
///
/// <para>
/// <b>Eligibility</b> (GetTaskDetailAsync): checked in-memory after the Task
/// service fetch; the service returns null (→ 404) for missing/wrong-tenant
/// tasks, and the in-memory gate covers intra-tenant IDOR.
/// </para>
/// </summary>
public sealed class MyTasksService : IMyTasksService
{
    private const int RoleQueuePerRoleFetchCap = 500;

    private readonly IFlowDbContext _db;
    private readonly IFlowUserContext _user;
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly ILogger<MyTasksService> _log;

    public MyTasksService(
        IFlowDbContext           db,
        IFlowUserContext         user,
        IFlowTaskServiceClient   taskClient,
        ILogger<MyTasksService>  log)
    {
        _db         = db;
        _user       = user;
        _taskClient = taskClient;
        _log        = log;
    }

    // =========================================================
    // My direct tasks
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListMyTasksAsync(MyTasksQuery query, CancellationToken ct = default)
    {
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("Authenticated user id is required to list My Tasks.");

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);

        // Single-status fast path: pass status filter to Task service so it can
        // apply it server-side. Multiple-status: fetch all, filter in memory.
        string? taskServiceStatus = null;
        if (query.Status is { Count: 1 })
            taskServiceStatus = ToTaskServiceStatus(NormaliseStatusFilter(query.Status)![0]);

        var raw = await _taskClient.ListTasksAsync(
            assignedUserId: userId,
            status:         taskServiceStatus,
            sort:           "flow_active_first",
            page:           page,
            pageSize:       pageSize,
            ct:             ct);

        // Multiple-status filter applied in memory (rare code path).
        IReadOnlyList<TaskServiceTaskDto> items = raw.Items;
        var totalCount = raw.Total;
        if (query.Status is { Count: > 1 })
        {
            var allowedStatuses = NormaliseStatusFilter(query.Status)!
                .Select(ToTaskServiceStatus)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            items     = items.Where(t => allowedStatuses.Contains(t.Status)).ToList();
            totalCount = items.Count;
        }

        var enriched = await EnrichAsync(items, ct);

        _log.LogDebug(
            "MyTasks query: UserId={UserId} Page={Page}/{PageSize} Total={Total} Returned={Returned}",
            userId, page, pageSize, totalCount, enriched.Count);

        return new PagedResponse<MyTaskDto>
        {
            Items      = enriched,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // Role Queue
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListRoleQueueAsync(RoleQueueQuery query, CancellationToken ct = default)
    {
        var roles           = _user.Roles;
        var isPlatformAdmin = _user.IsPlatformAdmin;

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);

        if (!isPlatformAdmin && (roles is null || roles.Count == 0))
            return EmptyPage(page, pageSize);

        List<TaskServiceTaskDto> mergedItems;
        int totalCount;

        if (isPlatformAdmin)
        {
            // Admins see all role-queue rows — single Task service call.
            var raw = await _taskClient.ListTasksAsync(
                assignmentMode: WorkflowTaskAssignmentMode.RoleQueue,
                status:         ToTaskServiceStatus(WorkflowTaskStatus.Open),
                sort:           "flow_active_first",
                page:           page,
                pageSize:       pageSize,
                ct:             ct);

            mergedItems = raw.Items.ToList();
            totalCount  = raw.Total;
        }
        else
        {
            // Per-role fetch: one call per role, capped at RoleQueuePerRoleFetchCap.
            // Dedupe by TaskId, sort in-memory (same urgency hierarchy), paginate.
            var seen = new HashSet<Guid>();
            var allItems = new List<TaskServiceTaskDto>();

            foreach (var role in roles!)
            {
                var raw = await _taskClient.ListTasksAsync(
                    assignmentMode: WorkflowTaskAssignmentMode.RoleQueue,
                    status:         ToTaskServiceStatus(WorkflowTaskStatus.Open),
                    assignedRole:   role,
                    sort:           "flow_active_first",
                    page:           1,
                    pageSize:       RoleQueuePerRoleFetchCap,
                    ct:             ct);

                foreach (var item in raw.Items)
                {
                    if (seen.Add(item.TaskId))
                        allItems.Add(item);
                }
            }

            // Sort merged set by the same urgency hierarchy and paginate.
            var sorted = SortByUrgency(allItems);
            totalCount  = sorted.Count;
            mergedItems = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        var enriched = await EnrichAsync(mergedItems, ct);

        _log.LogDebug(
            "RoleQueue query: UserId={UserId} IsPlatformAdmin={IsAdmin} Roles={RoleCount} Page={Page}/{PageSize} Total={Total}",
            _user.UserId, isPlatformAdmin, roles?.Count ?? 0, page, pageSize, totalCount);

        return new PagedResponse<MyTaskDto>
        {
            Items      = enriched,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // Org Queue
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListOrgQueueAsync(OrgQueueQuery query, CancellationToken ct = default)
    {
        var orgId           = _user.OrgId;
        var isPlatformAdmin = _user.IsPlatformAdmin;

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);

        if (!isPlatformAdmin && string.IsNullOrWhiteSpace(orgId))
            return EmptyPage(page, pageSize);

        var raw = await _taskClient.ListTasksAsync(
            assignmentMode: WorkflowTaskAssignmentMode.OrgQueue,
            status:         ToTaskServiceStatus(WorkflowTaskStatus.Open),
            assignedOrgId:  isPlatformAdmin ? null : orgId,
            sort:           "flow_active_first",
            page:           page,
            pageSize:       pageSize,
            ct:             ct);

        var enriched = await EnrichAsync(raw.Items, ct);

        _log.LogDebug(
            "OrgQueue query: UserId={UserId} OrgId={OrgId} IsPlatformAdmin={IsAdmin} Page={Page}/{PageSize} Total={Total}",
            _user.UserId, orgId, isPlatformAdmin, page, pageSize, raw.Total);

        return new PagedResponse<MyTaskDto>
        {
            Items      = enriched,
            TotalCount = raw.Total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // Task detail
    // =========================================================
    /// <summary>
    /// Returns a single task by id, with eligibility-scoped authorisation.
    ///
    /// <para>
    /// Eligibility (at least one must be true):
    ///   <list type="bullet">
    ///     <item>Platform admin.</item>
    ///     <item>DirectUser task assigned to the caller.</item>
    ///     <item>Open RoleQueue task for a role the caller holds.</item>
    ///     <item>Open OrgQueue task for the caller's org.</item>
    ///   </list>
    /// </para>
    ///
    /// <para>
    /// "Not found" and "not eligible" both surface as
    /// <see cref="NotFoundException"/> (→ 404) to prevent existence leakage.
    /// </para>
    /// </summary>
    public async Task<MyTaskDto> GetTaskDetailAsync(Guid taskId, CancellationToken ct = default)
    {
        var dto = await _taskClient.GetTaskByIdAsync(taskId, ct);

        if (dto is null)
            throw new NotFoundException(nameof(Domain.Entities.WorkflowTask), taskId);

        // In-memory eligibility check.
        if (!IsEligible(dto))
            throw new NotFoundException(nameof(Domain.Entities.WorkflowTask), taskId);

        // Enrich with workflow context.
        var enrichMap = await LoadWorkflowEnrichmentAsync(
            new[] { dto.WorkflowInstanceId }, ct);

        return MapToDto(dto, enrichMap);
    }

    // =========================================================
    // Helpers
    // =========================================================

    private bool IsEligible(TaskServiceTaskDto dto)
    {
        if (_user.IsPlatformAdmin) return true;

        var userId = _user.UserId;
        var orgId  = _user.OrgId;
        var roles  = _user.Roles ?? Array.Empty<string>();

        // DirectUser assignment to the caller.
        if (dto.AssignmentMode == WorkflowTaskAssignmentMode.DirectUser
            && dto.AssignedUserId != null
            && dto.AssignedUserId == userId)
            return true;

        // Open RoleQueue for a role the caller holds.
        if (dto.AssignmentMode == WorkflowTaskAssignmentMode.RoleQueue
            && dto.Status == ToTaskServiceStatus(WorkflowTaskStatus.Open)
            && dto.AssignedRole != null
            && roles.Contains(dto.AssignedRole, StringComparer.OrdinalIgnoreCase))
            return true;

        // Open OrgQueue for the caller's org.
        if (dto.AssignmentMode == WorkflowTaskAssignmentMode.OrgQueue
            && dto.Status == ToTaskServiceStatus(WorkflowTaskStatus.Open)
            && dto.AssignedOrgId != null
            && dto.AssignedOrgId == orgId)
            return true;

        return false;
    }

    private async Task<List<MyTaskDto>> EnrichAsync(
        IEnumerable<TaskServiceTaskDto> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return new();

        var enrichMap = await LoadWorkflowEnrichmentAsync(
            list.Select(t => t.WorkflowInstanceId), ct);

        return list.Select(t => MapToDto(t, enrichMap)).ToList();
    }

    /// <summary>
    /// Batch-loads WorkflowInstance Name + ProductKey keyed by Id.
    /// Uses <c>IgnoreQueryFilters</c> when resolving by GUID collection so
    /// cross-tenant data cannot leak (Task service already tenant-scoped the IDs).
    /// </summary>
    private async Task<Dictionary<Guid, (string? WorkflowName, string? ProductKey)>>
        LoadWorkflowEnrichmentAsync(IEnumerable<Guid?> workflowInstanceIds, CancellationToken ct)
    {
        var ids = workflowInstanceIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new();

        var rows = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(wi => ids.Contains(wi.Id))
            .Select(wi => new
            {
                wi.Id,
                wi.ProductKey,
                WorkflowName = wi.WorkflowDefinition != null ? wi.WorkflowDefinition.Name : null,
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => (r.WorkflowName, (string?)r.ProductKey));
    }

    private static MyTaskDto MapToDto(
        TaskServiceTaskDto dto,
        Dictionary<Guid, (string? WorkflowName, string? ProductKey)> enrichMap)
    {
        enrichMap.TryGetValue(dto.WorkflowInstanceId ?? Guid.Empty, out var wi);
        return new MyTaskDto
        {
            TaskId             = dto.TaskId,
            Title              = dto.Title,
            Description        = dto.Description,
            Status             = MapStatus(dto.Status),
            Priority           = MapPriority(dto.Priority),
            StepKey            = dto.WorkflowStepKey ?? string.Empty,

            AssignmentMode     = dto.AssignmentMode ?? WorkflowTaskAssignmentMode.Unassigned,
            AssignedUserId     = dto.AssignedUserId,
            AssignedRole       = dto.AssignedRole,
            AssignedOrgId      = dto.AssignedOrgId,
            AssignedAt         = dto.AssignedAt,
            AssignedBy         = dto.AssignedBy,
            AssignmentReason   = dto.AssignmentReason,

            CreatedAt          = dto.CreatedAtUtc,
            UpdatedAt          = dto.UpdatedAtUtc,
            StartedAt          = dto.StartedAt,
            CompletedAt        = dto.CompletedAt,
            CancelledAt        = dto.CancelledAt,

            DueAt              = dto.DueAt,
            SlaStatus          = dto.SlaStatus,
            SlaBreachedAt      = dto.SlaBreachedAt,

            WorkflowInstanceId = dto.WorkflowInstanceId ?? Guid.Empty,
            WorkflowName       = wi.WorkflowName,
            ProductKey         = wi.ProductKey,
        };
    }

    // ── Status string mapping ──────────────────────────────────────────────────

    /// <summary>Flow "Open" → Task service "OPEN" etc.</summary>
    private static string ToTaskServiceStatus(string flowStatus) =>
        flowStatus switch
        {
            WorkflowTaskStatus.Open       => "OPEN",
            WorkflowTaskStatus.InProgress => "IN_PROGRESS",
            WorkflowTaskStatus.Completed  => "COMPLETED",
            WorkflowTaskStatus.Cancelled  => "CANCELLED",
            _                             => flowStatus.ToUpperInvariant(),
        };

    /// <summary>Task service "OPEN" → Flow "Open" etc.</summary>
    private static string MapStatus(string taskServiceStatus) =>
        taskServiceStatus switch
        {
            "OPEN"        => WorkflowTaskStatus.Open,
            "IN_PROGRESS" => WorkflowTaskStatus.InProgress,
            "COMPLETED"   => WorkflowTaskStatus.Completed,
            "CANCELLED"   => WorkflowTaskStatus.Cancelled,
            _             => taskServiceStatus,
        };

    /// <summary>Task service "URGENT" → Flow "Urgent" etc.</summary>
    private static string MapPriority(string taskServicePriority) =>
        taskServicePriority switch
        {
            "URGENT" => WorkflowTaskPriority.Urgent,
            "HIGH"   => WorkflowTaskPriority.High,
            "NORMAL" => WorkflowTaskPriority.Normal,
            "LOW"    => WorkflowTaskPriority.Low,
            _        => taskServicePriority,
        };

    // ── In-memory sort (mirrors SQL ORDER BY from legacy service) ─────────────

    private static List<TaskServiceTaskDto> SortByUrgency(List<TaskServiceTaskDto> items)
    {
        static int SlaOrder(string s) =>
            s == WorkflowSlaStatus.Escalated ? 0 :
            s == WorkflowSlaStatus.Overdue   ? 1 :
            s == WorkflowSlaStatus.DueSoon   ? 2 :
            s == WorkflowSlaStatus.OnTrack   ? 3 : 4;

        static int PriorityOrder(string p) =>
            p == "URGENT" ? 0 :
            p == "HIGH"   ? 1 :
            p == "NORMAL" ? 2 :
            p == "LOW"    ? 3 : 2;

        static int ActiveOrder(string s) =>
            s is "OPEN" or "IN_PROGRESS" ? 0 : 1;

        return items
            .OrderBy(t => ActiveOrder(t.Status))
            .ThenBy(t => SlaOrder(t.SlaStatus))
            .ThenBy(t => PriorityOrder(t.Priority))
            .ThenBy(t => t.DueAt.HasValue ? 0 : 1)
            .ThenBy(t => t.DueAt)
            .ThenBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.TaskId)
            .ToList();
    }

    // ── Pagination helpers ────────────────────────────────────────────────────

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var p  = page < 1 ? 1 : page;
        var ps = pageSize < 1
            ? MyTasksDefaults.DefaultPageSize
            : Math.Min(pageSize, MyTasksDefaults.MaxPageSize);
        return (p, ps);
    }

    private static PagedResponse<MyTaskDto> EmptyPage(int page, int pageSize) => new()
    {
        Items      = Array.Empty<MyTaskDto>(),
        TotalCount = 0,
        Page       = page,
        PageSize   = pageSize,
    };

    private static IReadOnlyList<string>? NormaliseStatusFilter(IReadOnlyList<string>? status)
    {
        if (status is not { Count: > 0 }) return null;

        var cleaned = status
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var canonicalised = cleaned
            .Select(s =>
                s.Equals(WorkflowTaskStatus.Open,       StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Open       :
                s.Equals(WorkflowTaskStatus.InProgress, StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.InProgress :
                s.Equals(WorkflowTaskStatus.Completed,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Completed  :
                s.Equals(WorkflowTaskStatus.Cancelled,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Cancelled  :
                                                                                              s)
            .ToArray();

        var unknown = canonicalised
            .Where(s => !WorkflowTaskStatus.IsKnown(s))
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ValidationException(
                $"Unknown WorkflowTaskStatus value(s): {string.Join(", ", unknown)}. " +
                $"Allowed: Open, InProgress, Completed, Cancelled.");
        }

        return canonicalised;
    }
}
