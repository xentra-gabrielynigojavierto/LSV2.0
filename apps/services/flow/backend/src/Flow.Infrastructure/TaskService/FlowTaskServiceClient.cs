using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Flow.Application.Interfaces;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.TaskService;

/// <summary>
/// TASK-FLOW-01 / TASK-FLOW-02 — HTTP implementation of <see cref="IFlowTaskServiceClient"/>.
///
/// <para>
/// Calls the canonical Task service on behalf of Flow. All mutations here
/// make Task service the write authority; the caller is responsible for
/// mirroring the change to <c>flow_workflow_tasks</c> (the shadow) after
/// this client returns.
/// </para>
///
/// <para>
/// Two HttpClients are used:
///   <list type="bullet">
///     <item><c>_http</c> — typed client injected by DI; uses user bearer token
///       forwarding for user-facing Task service endpoints (AuthenticatedUser policy).</item>
///     <item><c>_internal</c> — named client "FlowTaskInternal"; always mints a
///       service token for internal endpoints (InternalService policy). Created
///       from <c>IHttpClientFactory</c>.</item>
///   </list>
/// </para>
///
/// <para>
/// Status mapping (Flow → Task service):
///   Open        → OPEN
///   InProgress  → IN_PROGRESS
///   Completed   → COMPLETED
///   Cancelled   → CANCELLED
/// </para>
///
/// <para>
/// Assignment model (TASK-FLOW-02 Phase 2):
///   All modes (DirectUser, RoleQueue, OrgQueue, Unassigned) are now forwarded to
///   the Task service via the internal <c>flow-queue-assign</c> endpoint.
///   Phase 1 limitation of DirectUser-only is removed.
/// </para>
/// </summary>
public sealed class FlowTaskServiceClient : IFlowTaskServiceClient
{
    private const string SourceProductCode = "SYNQ_FLOW";
    private const string TaskScope         = "WORKFLOW";

    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient                     _http;
    private readonly IHttpClientFactory             _clientFactory;
    private readonly ILogger<FlowTaskServiceClient> _log;

    public FlowTaskServiceClient(
        HttpClient                     http,
        IHttpClientFactory             clientFactory,
        ILogger<FlowTaskServiceClient> log)
    {
        _http          = http;
        _clientFactory = clientFactory;
        _log           = log;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient InternalClient => _clientFactory.CreateClient("FlowTaskInternal");

    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateWorkflowTaskAsync(
        Guid      workflowInstanceId,
        string    stepKey,
        string    title,
        string    priority,
        DateTime? dueAt,
        string?   assignedUserId,
        Guid?     externalId           = null,
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   assignedBy           = null,
        string?   assignmentReason     = null,
        CancellationToken ct = default)
    {
        Guid? assignedUserGuid = null;
        if (!string.IsNullOrWhiteSpace(assignedUserId))
        {
            if (Guid.TryParse(assignedUserId, out var parsed))
                assignedUserGuid = parsed;
            else
                _log.LogWarning(
                    "FlowTaskServiceClient.Create: AssignedUserId '{UserId}' is not a valid Guid — will create task unassigned in Task service.",
                    assignedUserId);
        }

        var body = new
        {
            title              = title,
            priority           = MapPriority(priority),
            scope              = TaskScope,
            assignedUserId     = assignedUserGuid,
            sourceProductCode  = SourceProductCode,
            dueAt              = dueAt,
            workflowInstanceId = workflowInstanceId,
            workflowStepKey    = stepKey,
            // TASK-FLOW-01 — shadow ID alignment: pass Flow's shadow task ID so
            // PlatformTask.Id == WorkflowTask.Id, allowing lifecycle delegates to
            // use the same Guid for both the shadow table and the Task service.
            externalId         = externalId,
            // TASK-FLOW-02 — queue assignment metadata
            assignmentMode     = assignmentMode,
            assignedRole       = assignedRole,
            assignedOrgId      = assignedOrgId,
            assignedBy         = assignedBy,
            assignmentReason   = assignmentReason,
        };

        var req  = BuildRequest(HttpMethod.Post, "/api/tasks", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, "POST /api/tasks");

        var dto = await resp.Content.ReadFromJsonAsync<TaskIdDto>(_json, ct)
            ?? throw new InvalidOperationException("Task service returned null body on task creation.");

        _log.LogInformation(
            "FlowTaskServiceClient.Create: created task {TaskId} for workflow={WorkflowInstanceId} step={StepKey} mode={Mode}",
            dto.Id, workflowInstanceId, stepKey, assignmentMode);

        return dto.Id;
    }

    // ── Status transitions ─────────────────────────────────────────────────────

    public Task StartTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "IN_PROGRESS", ct);

    public Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "COMPLETED", ct);

    public Task CancelTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(taskId, "CANCELLED", ct);

    private async Task TransitionAsync(Guid taskId, string newStatus, CancellationToken ct)
    {
        var body = new { status = newStatus };
        var req  = BuildRequest(HttpMethod.Post, $"/api/tasks/{taskId}/status", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"POST /api/tasks/{taskId}/status ({newStatus})");

        _log.LogInformation(
            "FlowTaskServiceClient.Transition: task {TaskId} → {NewStatus}", taskId, newStatus);
    }

    // ── Assignment (user bearer token) ─────────────────────────────────────────

    public async Task AssignUserAsync(Guid taskId, Guid? assignedUserId, CancellationToken ct = default)
    {
        var body = new { assignedUserId };
        var req  = BuildRequest(HttpMethod.Post, $"/api/tasks/{taskId}/assign", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"POST /api/tasks/{taskId}/assign");

        _log.LogInformation(
            "FlowTaskServiceClient.Assign: task {TaskId} → user {UserId}",
            taskId, assignedUserId?.ToString() ?? "(unassigned)");
    }

    // ── Internal: queue assignment (service token) ─────────────────────────────

    public async Task SetQueueAssignmentAsync(
        Guid      tenantId,
        Guid      taskId,
        string?   assignmentMode,
        Guid?     assignedUserId,
        string?   assignedRole,
        string?   assignedOrgId,
        string?   assignedBy,
        string?   assignmentReason,
        CancellationToken ct = default)
    {
        var body = new
        {
            assignmentMode   = assignmentMode,
            assignedUserId   = assignedUserId,
            assignedRole     = assignedRole,
            assignedOrgId    = assignedOrgId,
            assignedBy       = assignedBy,
            assignmentReason = assignmentReason,
        };

        var req = BuildInternalRequest(
            tenantId,
            HttpMethod.Post,
            $"/api/tasks/internal/flow-queue-assign/{tenantId}/{taskId}",
            body);

        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            _log.LogWarning(
                "FlowTaskServiceClient.SetQueueAssignment: task {TaskId} not found in tenant {TenantId} (404); shadow write will proceed.",
                taskId, tenantId);
            return;
        }

        EnsureSuccess(resp, $"POST /api/tasks/internal/flow-queue-assign/{tenantId}/{taskId}");

        _log.LogInformation(
            "FlowTaskServiceClient.SetQueueAssignment: task {TaskId} mode={Mode} user={UserId} role={Role} org={Org}",
            taskId, assignmentMode, assignedUserId, assignedRole, assignedOrgId);
    }

    // ── Internal: SLA push (service token) ────────────────────────────────────

    public async Task UpdateSlaStateAsync(
        Guid tenantId,
        IReadOnlyList<(Guid TaskId, string SlaStatus, DateTime? SlaBreachedAt, DateTime EvaluatedAt)> updates,
        CancellationToken ct = default)
    {
        if (updates.Count == 0) return;

        var body = new
        {
            updates = updates.Select(u => new
            {
                taskId        = u.TaskId,
                slaStatus     = u.SlaStatus,
                slaBreachedAt = u.SlaBreachedAt,
                evaluatedAt   = u.EvaluatedAt,
            }).ToArray()
        };

        var req    = BuildInternalRequest(tenantId, HttpMethod.Post, "/api/tasks/internal/flow-sla-update", body);
        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);
        EnsureSuccess(resp, "POST /api/tasks/internal/flow-sla-update");

        _log.LogInformation(
            "FlowTaskServiceClient.UpdateSlaState: pushed {Count} SLA updates for tenant {TenantId}",
            updates.Count, tenantId);
    }

    // ── Read: list tasks ───────────────────────────────────────────────────────

    public async Task<TaskServicePageResult> ListTasksAsync(
        string?   assignedUserId  = null,
        string?   status          = null,
        string?   assignmentMode  = null,
        string?   assignedRole    = null,
        string?   assignedOrgId   = null,
        string?   sort            = null,
        int       page            = 1,
        int       pageSize        = 50,
        CancellationToken ct      = default)
    {
        var qs = BuildQueryString(new Dictionary<string, string?>
        {
            ["assignedUserId"]  = assignedUserId,
            ["status"]          = status,
            ["assignmentMode"]  = assignmentMode,
            ["assignedRole"]    = assignedRole,
            ["assignedOrgId"]   = assignedOrgId,
            ["sort"]            = sort,
            ["page"]            = page.ToString(),
            ["pageSize"]        = pageSize.ToString(),
        });

        var req  = new HttpRequestMessage(HttpMethod.Get, $"/api/tasks{qs}");
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, "GET /api/tasks");

        var raw = await resp.Content.ReadFromJsonAsync<TaskListResponseRaw>(_json, ct)
            ?? throw new InvalidOperationException("Task service returned null body on list tasks.");

        return MapToPageResult(raw);
    }

    // ── Read: get by ID ────────────────────────────────────────────────────────

    public async Task<TaskServiceTaskDto?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var req  = new HttpRequestMessage(HttpMethod.Get, $"/api/tasks/{taskId}");
        var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(resp, $"GET /api/tasks/{taskId}");

        var raw = await resp.Content.ReadFromJsonAsync<TaskDtoRaw>(_json, ct);
        return raw is null ? null : MapToTaskDto(raw);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string MapPriority(string? priority) =>
        priority?.ToUpperInvariant() switch
        {
            "URGENT" => "URGENT",
            "HIGH"   => "HIGH",
            "LOW"    => "LOW",
            _        => "NORMAL",
        };

    private HttpRequestMessage BuildRequest<TBody>(HttpMethod method, string path, TBody? body = default)
    {
        var req = new HttpRequestMessage(method, path);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: _json);
        return req;
    }

    private HttpRequestMessage BuildInternalRequest<TBody>(
        Guid tenantId, HttpMethod method, string path, TBody? body = default)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
            req.Content = JsonContent.Create(body, options: _json);
        return req;
    }

    private static string BuildQueryString(Dictionary<string, string?> params_)
    {
        var pairs = params_
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");
        var qs = string.Join("&", pairs);
        return string.IsNullOrEmpty(qs) ? string.Empty : "?" + qs;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Task service call '{operation}' failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    private static TaskServicePageResult MapToPageResult(TaskListResponseRaw raw) =>
        new(
            Items:    (IReadOnlyList<TaskServiceTaskDto>?)
                          raw.Items?.Select(MapToTaskDto).ToList().AsReadOnly()
                      ?? Array.Empty<TaskServiceTaskDto>(),
            Total:    raw.Total,
            Page:     raw.Page,
            PageSize: raw.PageSize);

    private static TaskServiceTaskDto MapToTaskDto(TaskDtoRaw raw) =>
        new(
            TaskId:          raw.Id,
            TenantId:        raw.TenantId,
            Title:           raw.Title,
            Description:     raw.Description,
            Status:          raw.Status,
            Priority:        raw.Priority,
            WorkflowStepKey: raw.WorkflowStepKey,
            AssignmentMode:  raw.AssignmentMode,
            AssignedUserId:  raw.AssignedUserId?.ToString(),
            AssignedRole:    raw.AssignedRole,
            AssignedOrgId:   raw.AssignedOrgId,
            AssignedAt:      raw.AssignedAt,
            AssignedBy:      raw.AssignedBy,
            AssignmentReason: raw.AssignmentReason,
            CreatedAtUtc:    raw.CreatedAtUtc,
            UpdatedAtUtc:    raw.UpdatedAtUtc,
            StartedAt:       raw.StartedAt,
            CompletedAt:     raw.CompletedAt,
            CancelledAt:     raw.CancelledAt,
            DueAt:           raw.DueAt,
            SlaStatus:       raw.SlaStatus ?? "OnTrack",
            SlaBreachedAt:   raw.SlaBreachedAt,
            WorkflowInstanceId: raw.WorkflowInstanceId);

    // ── Private DTOs (minimal projection for deserialization) ─────────────────

    private sealed record TaskIdDto(Guid Id);

    private sealed record TaskDtoRaw(
        Guid      Id,
        Guid      TenantId,
        string    Title,
        string?   Description,
        string    Status,
        string    Priority,
        string?   WorkflowStepKey,
        string?   AssignmentMode,
        Guid?     AssignedUserId,
        string?   AssignedRole,
        string?   AssignedOrgId,
        DateTime? AssignedAt,
        string?   AssignedBy,
        string?   AssignmentReason,
        DateTime  CreatedAtUtc,
        DateTime  UpdatedAtUtc,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        DateTime? CancelledAt,
        DateTime? DueAt,
        string?   SlaStatus,
        DateTime? SlaBreachedAt,
        Guid?     WorkflowInstanceId);

    private sealed record TaskListResponseRaw(
        IReadOnlyList<TaskDtoRaw>? Items,
        int Total,
        int Page,
        int PageSize);

    // ── Read: analytics (TASK-FLOW-04) ────────────────────────────────────────

    public async Task<TaskSlaAnalyticsResult> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default)
    {
        var qs  = BuildQueryString(new Dictionary<string, string?>
        {
            ["windowStart"] = windowStart.ToString("O"),
            ["windowEnd"]   = windowEnd.ToString("O"),
        });
        var req  = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-analytics/{tenantId}/sla{qs}");
        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-analytics/{tenantId}/sla");

        var raw = await resp.Content.ReadFromJsonAsync<SlaAnalyticsRaw>(_json, ct)
            ?? throw new InvalidOperationException("Task analytics: null SLA response.");

        return new TaskSlaAnalyticsResult(
            SlaGroups:               (raw.SlaGroups ?? []).Select(g => new SlaStatusCount(g.SlaStatus, g.Count)).ToList(),
            AvgOverdueAgeDays:       raw.AvgOverdueAgeDays,
            BreachedInWindow:        raw.BreachedInWindow,
            CompletedInWindow:       raw.CompletedInWindow,
            CompletedOnTimeInWindow: raw.CompletedOnTimeInWindow,
            RoleOverdueGroups:       (raw.RoleOverdueGroups ?? []).Select(g => new QueueOverdueItem(g.QueueKey, g.OverdueCount)).ToList(),
            OrgOverdueGroups:        (raw.OrgOverdueGroups  ?? []).Select(g => new QueueOverdueItem(g.QueueKey, g.OverdueCount)).ToList());
    }

    public async Task<TaskQueueAnalyticsResult> GetQueueAnalyticsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var req    = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-analytics/{tenantId}/queue");
        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-analytics/{tenantId}/queue");

        var raw = await resp.Content.ReadFromJsonAsync<QueueAnalyticsRaw>(_json, ct)
            ?? throw new InvalidOperationException("Task analytics: null queue response.");

        return new TaskQueueAnalyticsResult(
            RoleGroups:          (raw.RoleGroups ?? []).Select(g => new QueueGroupItem(g.Key, g.Status, g.SlaStatus, g.Count)).ToList(),
            OrgGroups:           (raw.OrgGroups  ?? []).Select(g => new QueueGroupItem(g.Key, g.Status, g.SlaStatus, g.Count)).ToList(),
            UnassignedCount:     raw.UnassignedCount,
            OldestQueueAgeHours: raw.OldestQueueAgeHours,
            MedianQueueAgeHours: raw.MedianQueueAgeHours,
            ActiveUserCount:     raw.ActiveUserCount,
            OverloadedUserCount: raw.OverloadedUserCount);
    }

    public async Task<TaskAssignmentAnalyticsResult> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default)
    {
        var qs  = BuildQueryString(new Dictionary<string, string?>
        {
            ["windowStart"] = windowStart.ToString("O"),
            ["windowEnd"]   = windowEnd.ToString("O"),
        });
        var req    = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-analytics/{tenantId}/assignment{qs}");
        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-analytics/{tenantId}/assignment");

        var raw = await resp.Content.ReadFromJsonAsync<AssignmentAnalyticsRaw>(_json, ct)
            ?? throw new InvalidOperationException("Task analytics: null assignment response.");

        return new TaskAssignmentAnalyticsResult(
            ModeGroups:       (raw.ModeGroups       ?? []).Select(g => new ModeCount(g.Mode, g.Count)).ToList(),
            AssignedInWindow: raw.AssignedInWindow,
            UserStatusGroups: (raw.UserStatusGroups ?? []).Select(g => new UserStatusCount(g.UserId, g.Status, g.Count)).ToList());
    }

    public async Task<TaskPlatformAnalyticsResult> GetPlatformAnalyticsAsync(
        CancellationToken ct = default)
    {
        var req    = new HttpRequestMessage(HttpMethod.Get,
            "/api/tasks/internal/flow-analytics/platform-summary");
        var client = InternalClient;
        var resp   = await client.SendAsync(req, ct);
        EnsureSuccess(resp, "GET /api/tasks/internal/flow-analytics/platform-summary");

        var raw = await resp.Content.ReadFromJsonAsync<PlatformAnalyticsRaw>(_json, ct)
            ?? throw new InvalidOperationException("Task analytics: null platform response.");

        return new TaskPlatformAnalyticsResult(
            TotalActiveTasks:  raw.TotalActiveTasks,
            TotalOverdueTasks: raw.TotalOverdueTasks,
            TenantSlaGroups:   (raw.TenantSlaGroups ?? []).Select(g => new TenantSlaCount(g.TenantId, g.SlaStatus, g.Count)).ToList());
    }

    // ── Analytics deserialization records (TASK-FLOW-04) ──────────────────────

    private sealed record SlaStatusCountRaw(string SlaStatus, int Count);
    private sealed record QueueOverdueRaw(string QueueKey, int OverdueCount);
    private sealed record SlaAnalyticsRaw(
        IReadOnlyList<SlaStatusCountRaw>? SlaGroups,
        double?                           AvgOverdueAgeDays,
        int                               BreachedInWindow,
        int                               CompletedInWindow,
        int                               CompletedOnTimeInWindow,
        IReadOnlyList<QueueOverdueRaw>?   RoleOverdueGroups,
        IReadOnlyList<QueueOverdueRaw>?   OrgOverdueGroups);

    private sealed record QueueGroupRaw(string Key, string Status, string SlaStatus, int Count);
    private sealed record QueueAnalyticsRaw(
        IReadOnlyList<QueueGroupRaw>? RoleGroups,
        IReadOnlyList<QueueGroupRaw>? OrgGroups,
        int                           UnassignedCount,
        double?                       OldestQueueAgeHours,
        double?                       MedianQueueAgeHours,
        int                           ActiveUserCount,
        int                           OverloadedUserCount);

    private sealed record ModeCountRaw(string? Mode, int Count);
    private sealed record UserStatusCountRaw(string UserId, string Status, int Count);
    private sealed record AssignmentAnalyticsRaw(
        IReadOnlyList<ModeCountRaw>?       ModeGroups,
        int                                AssignedInWindow,
        IReadOnlyList<UserStatusCountRaw>? UserStatusGroups);

    private sealed record TenantSlaCountRaw(Guid TenantId, string SlaStatus, int Count);
    private sealed record PlatformAnalyticsRaw(
        int                                TotalActiveTasks,
        int                                TotalOverdueTasks,
        IReadOnlyList<TenantSlaCountRaw>?  TenantSlaGroups);

    // ── Workload / dedup (TASK-FLOW-03) ───────────────────────────────────────

    public async Task<IReadOnlyDictionary<string, int>> GetWorkloadCountsAsync(
        Guid                tenantId,
        IEnumerable<string> userIds,
        CancellationToken   ct = default)
    {
        var ids = string.Join(",", userIds);
        if (string.IsNullOrEmpty(ids))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var qs  = BuildQueryString(new Dictionary<string, string?> { ["userIds"] = ids });
        var req = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-workload/{tenantId}/user-counts{qs}");
        var resp = await InternalClient.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-workload/{tenantId}/user-counts");

        var rows = await resp.Content.ReadFromJsonAsync<IReadOnlyList<WorkloadCountRaw>>(_json, ct)
            ?? [];

        return rows.ToDictionary(
            r => r.UserId,
            r => r.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetWorkloadUsersByRoleAsync(
        Guid              tenantId,
        string            role,
        int               max = 20,
        CancellationToken ct  = default)
    {
        var qs   = BuildQueryString(new Dictionary<string, string?> { ["max"] = max.ToString() });
        var req  = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-workload/{tenantId}/role/{Uri.EscapeDataString(role)}/users{qs}");
        var resp = await InternalClient.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-workload/{tenantId}/role/users");

        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<string>>(_json, ct)
            ?? Array.Empty<string>();
    }

    public async Task<IReadOnlyList<string>> GetWorkloadUsersByOrgAsync(
        Guid              tenantId,
        string            orgId,
        int               max = 20,
        CancellationToken ct  = default)
    {
        var qs   = BuildQueryString(new Dictionary<string, string?> { ["max"] = max.ToString() });
        var req  = BuildInternalRequest<object?>(tenantId, HttpMethod.Get,
            $"/api/tasks/internal/flow-workload/{tenantId}/org/{Uri.EscapeDataString(orgId)}/users{qs}");
        var resp = await InternalClient.SendAsync(req, ct);
        EnsureSuccess(resp, $"GET /api/tasks/internal/flow-workload/{tenantId}/org/users");

        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<string>>(_json, ct)
            ?? Array.Empty<string>();
    }

    public async Task<bool> HasActiveStepTaskAsync(
        Guid              tenantId,
        Guid              workflowInstanceId,
        string            stepKey,
        CancellationToken ct = default)
    {
        var qs  = BuildQueryString(new Dictionary<string, string?>
        {
            ["tenantId"]           = tenantId.ToString(),
            ["workflowInstanceId"] = workflowInstanceId.ToString(),
            ["stepKey"]            = stepKey,
        });
        var req  = new HttpRequestMessage(HttpMethod.Get,
            $"/api/tasks/internal/flow-has-active-step{qs}");
        var resp = await InternalClient.SendAsync(req, ct);
        EnsureSuccess(resp, "GET /api/tasks/internal/flow-has-active-step");

        var raw = await resp.Content.ReadFromJsonAsync<HasActiveStepRaw>(_json, ct);
        return raw?.HasActive ?? false;
    }

    // TASK-FLOW-03 — SLA batch read for WorkflowTaskSlaEvaluator.
    // Calls the cross-tenant internal endpoint; no tenantId required.
    public async Task<IReadOnlyList<FlowSlaBatchItem>> GetTasksForSlaEvaluationAsync(
        int               batchSize,
        int               dueSoonThresholdMinutes,
        CancellationToken ct = default)
    {
        var qs = BuildQueryString(new Dictionary<string, string?>
        {
            ["batchSize"]      = Math.Max(1, batchSize).ToString(),
            ["dueSoonMinutes"] = Math.Max(0, dueSoonThresholdMinutes).ToString(),
        });
        var req  = new HttpRequestMessage(HttpMethod.Get,
            $"/api/tasks/internal/flow-sla-batch{qs}");
        var resp = await InternalClient.SendAsync(req, ct);
        EnsureSuccess(resp, "GET /api/tasks/internal/flow-sla-batch");

        var raw = await resp.Content.ReadFromJsonAsync<FlowSlaBatchRaw>(_json, ct);
        if (raw?.Items is null) return Array.Empty<FlowSlaBatchItem>();

        return raw.Items
            .Select(r => new FlowSlaBatchItem(
                r.TaskId,
                r.TenantId,
                r.DueAt,
                r.SlaStatus,
                r.SlaBreachedAt))
            .ToList();
    }

    private sealed record WorkloadCountRaw(string UserId, int Count);
    private sealed record HasActiveStepRaw(bool HasActive);

    private sealed record FlowSlaBatchRaw(IReadOnlyList<FlowSlaBatchItemRaw>? Items);
    private sealed record FlowSlaBatchItemRaw(
        Guid      TaskId,
        Guid      TenantId,
        DateTime? DueAt,
        string    SlaStatus,
        DateTime? SlaBreachedAt);
}
