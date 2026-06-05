using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.TaskService;

/// <summary>
/// TASK-B04 / TASK-010 — HTTP client implementation for the canonical Task service.
/// All Liens task runtime is delegated to this client; Liens no longer owns task storage.
/// Status bridging: Liens "NEW" ↔ Task "OPEN" (all others pass through 1-to-1).
/// </summary>
public sealed class LiensTaskServiceClient : ILiensTaskServiceClient
{
    private const string SourceProductCode  = "SYNQ_LIENS";
    private const string SourceEntityType   = "LIEN_CASE";
    private const string LinkedEntityType   = "LIEN";
    private const string LinkedRelationship = "RELATED";
    private const string TaskScope          = "PRODUCT";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient               _http;
    private readonly ILogger<LiensTaskServiceClient> _logger;

    public LiensTaskServiceClient(HttpClient http, ILogger<LiensTaskServiceClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ── Status bridging ─────────────────────────────────────────────────────────

    private static string ToTaskStatus(string liensStatus) =>
        string.Equals(liensStatus, "NEW", StringComparison.OrdinalIgnoreCase) ? "OPEN" : liensStatus.ToUpperInvariant();

    private static string ToLiensStatus(string taskStatus) =>
        string.Equals(taskStatus, "OPEN", StringComparison.OrdinalIgnoreCase) ? "NEW" : taskStatus.ToUpperInvariant();

    // ── Create ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> CreateTaskAsync(
        Guid              tenantId,
        Guid              actingUserId,
        Guid              externalId,
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            title                = request.Title,
            description          = request.Description,
            priority             = request.Priority,
            scope                = TaskScope,
            assignedUserId       = request.AssignedUserId,
            sourceProductCode    = SourceProductCode,
            sourceEntityType     = request.CaseId.HasValue ? SourceEntityType : (string?)null,
            sourceEntityId       = request.CaseId,
            dueAt                = request.DueDate,
            workflowInstanceId   = (Guid?)null,
            workflowStepKey      = (string?)null,
            externalId           = (Guid?)externalId,
            // TASK-B04-01 — generation provenance for duplicate-prevention
            generationRuleId     = request.GenerationRuleId,
            generatingTemplateId = request.GeneratingTemplateId,
        };

        var response = await PostAsync<object, TaskApiDto>(
            tenantId, "/api/tasks", body, actingUserId, ct);

        if (response is null)
            throw new InvalidOperationException("Task service returned null on create.");

        var result = MapToTaskResponse(response);

        // Add lien linked entities
        foreach (var lienId in request.LienIds)
        {
            await AddLinkedEntityAsync(tenantId, result.Id, actingUserId,
                LinkedEntityType, lienId, LinkedRelationship, ct);
        }

        return result;
    }

    // ── Get ──────────────────────────────────────────────────────────────────────

    public async Task<TaskResponse?> GetTaskAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var dto = await GetAsync<TaskApiDto>(tenantId, $"/api/tasks/{id}", ct);
        return dto is null ? null : MapToTaskResponse(dto);
    }

    // ── Search ───────────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<TaskResponse>> SearchTasksAsync(
        Guid    tenantId,
        string? search,
        string? status,
        string? priority,
        Guid?   assignedUserId,
        Guid?   caseId,
        Guid?   lienId,
        Guid?   workflowStageId,
        string? assignmentScope,
        Guid?   currentUserId,
        int     page,
        int     pageSize,
        CancellationToken ct = default)
    {
        var qs = BuildSearchQuery(search, status, priority, assignedUserId,
                                  caseId, lienId, workflowStageId,
                                  assignmentScope, currentUserId, page, pageSize);

        var list = await GetAsync<TaskListApiDto>(tenantId, $"/api/tasks?{qs}", ct);
        if (list is null)
            return new PaginatedResult<TaskResponse> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };

        return new PaginatedResult<TaskResponse>
        {
            Items      = list.Items.Select(MapToTaskResponse).ToList(),
            TotalCount = list.Total,
            Page       = list.Page,
            PageSize   = list.PageSize,
        };
    }

    private static string BuildSearchQuery(
        string? search, string? status, string? priority,
        Guid? assignedUserId, Guid? caseId, Guid? lienId,
        Guid? workflowStageId, string? assignmentScope, Guid? currentUserId,
        int page, int pageSize)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"search={Uri.EscapeDataString(search)}");

        if (!string.IsNullOrWhiteSpace(status))
            parts.Add($"status={Uri.EscapeDataString(ToTaskStatus(status))}");

        if (!string.IsNullOrWhiteSpace(priority))
            parts.Add($"priority={Uri.EscapeDataString(priority)}");

        if (assignedUserId.HasValue)
            parts.Add($"assignedUserId={assignedUserId.Value}");

        parts.Add($"sourceProductCode={SourceProductCode}");

        if (caseId.HasValue)
        {
            parts.Add($"sourceEntityType={SourceEntityType}");
            parts.Add($"sourceEntityId={caseId.Value}");
        }

        if (lienId.HasValue)
        {
            parts.Add($"linkedEntityType={LinkedEntityType}");
            parts.Add($"linkedEntityId={lienId.Value}");
        }

        if (workflowStageId.HasValue)
            parts.Add($"stageId={workflowStageId.Value}");

        if (!string.IsNullOrWhiteSpace(assignmentScope))
            parts.Add($"assignmentScope={Uri.EscapeDataString(assignmentScope)}");

        parts.Add($"page={page}");
        parts.Add($"pageSize={pageSize}");

        return string.Join("&", parts);
    }

    // ── TASK-B04-01 — Duplicate-prevention helpers ────────────────────────────────

    public async Task<bool> HasOpenTaskForRuleAsync(
        Guid  tenantId,
        Guid  ruleId,
        Guid? caseId,
        Guid? lienId,
        CancellationToken ct = default)
    {
        var qs = BuildDupCheckQuery(
            ruleId, null, caseId, lienId);
        if (qs is null) return false;

        var list = await GetAsync<TaskListApiDto>(tenantId, $"/api/tasks?{qs}", ct);
        return list is { Total: > 0 };
    }

    public async Task<bool> HasOpenTaskForTemplateAsync(
        Guid  tenantId,
        Guid  templateId,
        Guid? caseId,
        Guid? lienId,
        CancellationToken ct = default)
    {
        var qs = BuildDupCheckQuery(
            null, templateId, caseId, lienId);
        if (qs is null) return false;

        var list = await GetAsync<TaskListApiDto>(tenantId, $"/api/tasks?{qs}", ct);
        return list is { Total: > 0 };
    }

    /// <summary>
    /// Builds a minimal query string for the duplicate-prevention search.
    /// Exactly one of <paramref name="ruleId"/> / <paramref name="templateId"/> must be set.
    /// Returns null if neither entity scope (caseId nor lienId) is provided.
    /// </summary>
    private static string? BuildDupCheckQuery(
        Guid? ruleId, Guid? templateId, Guid? caseId, Guid? lienId)
    {
        if (!caseId.HasValue && !lienId.HasValue)
            return null;

        var parts = new List<string>
        {
            $"sourceProductCode={SourceProductCode}",
            "excludeTerminal=true",
            "pageSize=1",
            "page=1",
        };

        if (ruleId.HasValue)
            parts.Add($"generationRuleId={ruleId.Value}");
        else if (templateId.HasValue)
            parts.Add($"generatingTemplateId={templateId.Value}");

        if (caseId.HasValue)
        {
            parts.Add($"sourceEntityType={SourceEntityType}");
            parts.Add($"sourceEntityId={caseId.Value}");
        }
        else if (lienId.HasValue)
        {
            parts.Add($"linkedEntityType={LinkedEntityType}");
            parts.Add($"linkedEntityId={lienId.Value}");
        }

        return string.Join("&", parts);
    }

    // ── Update ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> UpdateTaskAsync(
        Guid              tenantId,
        Guid              id,
        Guid              actingUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            title       = request.Title,
            description = request.Description,
            priority    = request.Priority,
            dueAt       = request.DueDate,
        };

        var dto = await PutAsync<object, TaskApiDto>(
            tenantId, $"/api/tasks/{id}", body, actingUserId, ct);

        if (dto is null)
            throw new InvalidOperationException("Task service returned null on update.");

        return MapToTaskResponse(dto);
    }

    // ── Assign ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> AssignTaskAsync(
        Guid  tenantId,
        Guid  id,
        Guid  actingUserId,
        Guid? assignedUserId,
        CancellationToken ct = default)
    {
        var body = new { assignedUserId };
        var dto  = await PostAsync<object, TaskApiDto>(
            tenantId, $"/api/tasks/{id}/assign", body, actingUserId, ct);

        if (dto is null)
            throw new InvalidOperationException("Task service returned null on assign.");

        return MapToTaskResponse(dto);
    }

    // ── Status transition ────────────────────────────────────────────────────────

    public async Task<TaskResponse> TransitionStatusAsync(
        Guid   tenantId,
        Guid   id,
        Guid   actingUserId,
        string newStatus,
        CancellationToken ct = default)
    {
        var body = new { status = ToTaskStatus(newStatus) };
        var dto  = await PostAsync<object, TaskApiDto>(
            tenantId, $"/api/tasks/{id}/status", body, actingUserId, ct);

        if (dto is null)
            throw new InvalidOperationException("Task service returned null on status transition.");

        return MapToTaskResponse(dto);
    }

    // ── Notes ────────────────────────────────────────────────────────────────────

    public async Task<TaskNoteResponse> AddNoteAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   actorUserId,
        string content,
        string createdByName,
        CancellationToken ct = default)
    {
        var body = new { note = content, authorName = createdByName };
        var dto  = await PostAsync<object, TaskNoteApiDto>(
            tenantId, $"/api/tasks/{taskId}/notes", body, actorUserId, ct);

        if (dto is null)
            throw new InvalidOperationException("Task service returned null on add note.");

        return MapToNoteResponse(dto);
    }

    public async Task<List<TaskNoteResponse>> GetNotesAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default)
    {
        var dtos = await GetAsync<List<TaskNoteApiDto>>(tenantId, $"/api/tasks/{taskId}/notes", ct);
        return dtos?.Select(MapToNoteResponse).ToList() ?? [];
    }

    public async Task<TaskNoteResponse> UpdateNoteAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   noteId,
        Guid   actorUserId,
        string content,
        CancellationToken ct = default)
    {
        var body = new { note = content };
        var dto  = await PutAsync<object, TaskNoteApiDto>(
            tenantId, $"/api/tasks/{taskId}/notes/{noteId}", body, actorUserId, ct);

        if (dto is null)
            throw new InvalidOperationException("Task service returned null on update note.");

        return MapToNoteResponse(dto);
    }

    public async Task DeleteNoteAsync(
        Guid tenantId,
        Guid taskId,
        Guid noteId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var req = BuildRequest(tenantId, actorUserId, HttpMethod.Delete,
            $"/api/tasks/{taskId}/notes/{noteId}");

        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"DELETE /api/tasks/{taskId}/notes/{noteId}");
    }

    // ── Linked entity ────────────────────────────────────────────────────────────

    public async Task AddLinkedEntityAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   actingUserId,
        string entityType,
        Guid   entityId,
        string relationship,
        CancellationToken ct = default)
    {
        var body = new { entityType, entityId = entityId.ToString(), relationshipType = relationship, sourceProductCode = SourceProductCode };
        await PostAsync<object, object>(
            tenantId, $"/api/tasks/{taskId}/linked-entities", body, actingUserId, ct);
    }

    // ── Flow callback ────────────────────────────────────────────────────────────

    public async Task TriggerFlowCallbackAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string newStepKey,
        CancellationToken ct = default)
    {
        var body = new { workflowInstanceId, newStepKey, tenantId };
        var req  = BuildRequest(tenantId, null, HttpMethod.Post,
            "/api/tasks/internal/flow-callback", body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, "POST /api/tasks/internal/flow-callback");
    }

    // ── Backfill ─────────────────────────────────────────────────────────────────

    public async Task<BackfillTaskResult> BackfillTaskAsync(
        Guid   tenantId,
        Guid   actingUserId,
        Guid   externalId,
        CreateTaskRequest request,
        IReadOnlyList<(Guid NoteId, string Content, string AuthorName, Guid AuthorId, DateTime CreatedAt)> notes,
        IReadOnlyList<Guid> lienIds,
        CancellationToken ct = default)
    {
        // Idempotency: check if task with this ID already exists
        var existing = await GetTaskAsync(tenantId, externalId, ct);
        if (existing is not null)
        {
            return new BackfillTaskResult(existing.Id, 0, 0, AlreadyExisted: true);
        }

        // Create task with deterministic external ID
        var taskResponse = await CreateTaskAsync(tenantId, actingUserId, externalId, request, ct);
        var taskId       = taskResponse.Id;

        // Backfill notes (preserve order / author attribution)
        var notesCreated = 0;
        foreach (var (_, content, authorName, authorId, _) in notes)
        {
            try
            {
                await AddNoteAsync(tenantId, taskId, authorId, content, authorName, ct);
                notesCreated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "BackfillTask: failed to create note on task {TaskId}", taskId);
            }
        }

        // Additional lien links (beyond the ones in CreateTaskRequest.LienIds)
        var linksCreated = 0;
        foreach (var lienId in lienIds)
        {
            if (request.LienIds.Contains(lienId)) continue;
            try
            {
                await AddLinkedEntityAsync(tenantId, taskId, actingUserId,
                    LinkedEntityType, lienId, LinkedRelationship, ct);
                linksCreated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "BackfillTask: failed to add lien link for LienId={LienId} on task {TaskId}", lienId, taskId);
            }
        }

        return new BackfillTaskResult(taskId, notesCreated, linksCreated + request.LienIds.Count, AlreadyExisted: false);
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────────

    private async Task<TResponse?> GetAsync<TResponse>(
        Guid   tenantId,
        string path,
        CancellationToken ct)
    {
        var req  = BuildRequest(tenantId, null, HttpMethod.Get, path);
        var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound) return default;
        EnsureSuccess(resp, $"GET {path}");
        return await resp.Content.ReadFromJsonAsync<TResponse>(_json, ct);
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        Guid   tenantId,
        string path,
        TRequest body,
        Guid actingUserId,
        CancellationToken ct)
    {
        var req  = BuildRequest(tenantId, actingUserId, HttpMethod.Post, path, body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"POST {path}");
        return typeof(TResponse) == typeof(object)
            ? default
            : await resp.Content.ReadFromJsonAsync<TResponse>(_json, ct);
    }

    private async Task<TResponse?> PutAsync<TRequest, TResponse>(
        Guid   tenantId,
        string path,
        TRequest body,
        Guid actingUserId,
        CancellationToken ct)
    {
        var req  = BuildRequest(tenantId, actingUserId, HttpMethod.Put, path, body);
        var resp = await _http.SendAsync(req, ct);
        EnsureSuccess(resp, $"PUT {path}");
        return await resp.Content.ReadFromJsonAsync<TResponse>(_json, ct);
    }

    private HttpRequestMessage BuildRequest<TBody>(
        Guid       tenantId,
        Guid?      actingUserId,
        HttpMethod method,
        string     path,
        TBody?     body = default)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        if (actingUserId.HasValue)
            req.Headers.Add("X-User-Id", actingUserId.Value.ToString());

        if (body is not null)
            req.Content = JsonContent.Create(body, options: _json);

        return req;
    }

    private HttpRequestMessage BuildRequest(
        Guid       tenantId,
        Guid?      actingUserId,
        HttpMethod method,
        string     path)
        => BuildRequest<object>(tenantId, actingUserId, method, path, null);

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Task service call '{operation}' failed with status {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    // ── Response mapping ─────────────────────────────────────────────────────────

    private TaskResponse MapToTaskResponse(TaskApiDto dto) => new()
    {
        Id                  = dto.Id,
        TenantId            = dto.TenantId,
        Title               = dto.Title,
        Description         = dto.Description,
        Status              = ToLiensStatus(dto.Status),
        Priority            = dto.Priority,
        AssignedUserId      = dto.AssignedUserId,
        CaseId              = dto.SourceEntityId,
        WorkflowStageId     = dto.CurrentStageId,
        DueDate             = dto.DueAt,
        CompletedAt         = dto.CompletedAt,
        ClosedByUserId      = dto.ClosedByUserId,
        LinkedLiens         = [],
        CreatedByUserId     = dto.CreatedByUserId,
        CreatedAtUtc        = dto.CreatedAtUtc,
        UpdatedAtUtc        = dto.UpdatedAtUtc,
        SourceType          = "SYNQ_LIENS",
        IsSystemGenerated   = false,
        WorkflowInstanceId  = dto.WorkflowInstanceId,
        WorkflowStepKey     = dto.WorkflowStepKey,
    };

    private static TaskNoteResponse MapToNoteResponse(TaskNoteApiDto dto) => new()
    {
        Id              = dto.Id,
        TaskId          = dto.TaskId,
        Content         = dto.Note,
        CreatedByUserId = dto.CreatedByUserId ?? Guid.Empty,
        CreatedByName   = dto.AuthorName ?? string.Empty,
        IsEdited        = dto.IsEdited,
        CreatedAtUtc    = dto.CreatedAtUtc,
        UpdatedAtUtc    = null,
    };

    // ── TASK-MIG-01 — Governance round-trip ──────────────────────────────────────

    public async Task<TaskServiceGovernanceResponse?> GetGovernanceAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/governance?sourceProductCode={Uri.EscapeDataString(productCode)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);

        if (response.StatusCode == HttpStatusCode.NoContent ||
            response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<TaskServiceGovernanceResponse>(_json, ct);
        return dto;
    }

    public async System.Threading.Tasks.Task UpsertGovernanceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceGovernanceUpsertRequest payload,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tasks/governance")
        {
            Content = JsonContent.Create(payload, options: _json),
        };
        req.Headers.Add("X-Tenant-Id",   tenantId.ToString());
        req.Headers.Add("X-User-Id",     actingUserId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── TASK-MIG-02 — Template round-trip ────────────────────────────────────────

    public async Task<TaskServiceTemplateResponse?> GetTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/templates/{templateId}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);

        if (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TaskServiceTemplateResponse>(_json, ct);
    }

    public async Task<List<TaskServiceTemplateResponse>> GetAllTemplatesAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/templates?sourceProductCode={Uri.EscapeDataString(productCode)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var list = await response.Content
            .ReadFromJsonAsync<List<TaskServiceTemplateResponse>>(_json, ct);
        return list ?? [];
    }

    public async System.Threading.Tasks.Task UpsertTemplateFromSourceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceTemplateUpsertRequest payload,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tasks/templates/from-source")
        {
            Content = JsonContent.Create(payload, options: _json),
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id",   actingUserId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── TASK-MIG-03 — Stage config round-trip ────────────────────────────────────

    public async Task<TaskServiceStageResponse?> GetStageAsync(
        Guid tenantId,
        Guid stageId,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/stages/{stageId}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);

        if (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TaskServiceStageResponse>(_json, ct);
    }

    public async Task<List<TaskServiceStageResponse>> GetAllStagesAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/stages?sourceProductCode={Uri.EscapeDataString(productCode)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var list = await response.Content
            .ReadFromJsonAsync<List<TaskServiceStageResponse>>(_json, ct);
        return list ?? [];
    }

    public async System.Threading.Tasks.Task UpsertStageFromSourceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceStageUpsertRequest payload,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tasks/stages/from-source")
        {
            Content = JsonContent.Create(payload, options: _json),
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id",   actingUserId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── TASK-MIG-04 — Transition round-trip ──────────────────────────────────────

    public async Task<List<TaskServiceTransitionResponse>> GetTransitionsAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default)
    {
        var url = $"/api/tasks/stage-transitions?productCode={Uri.EscapeDataString(productCode)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();

        var list = await response.Content
            .ReadFromJsonAsync<List<TaskServiceTransitionResponse>>(_json, ct);
        return list ?? [];
    }

    public async System.Threading.Tasks.Task UpsertTransitionsFromSourceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceTransitionsUpsertRequest payload,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post, "/api/tasks/stage-transitions/from-source")
        {
            Content = JsonContent.Create(payload, options: _json),
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id",   actingUserId.ToString());

        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── History ───────────────────────────────────────────────────────────────────

    public async Task<List<TaskHistoryEventDto>> GetHistoryAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default)
    {
        var dtos = await GetAsync<List<TaskHistoryApiDto>>(tenantId, $"/api/tasks/{taskId}/history", ct);
        return dtos?.Select(MapToHistoryDto).ToList() ?? [];
    }

    private static TaskHistoryEventDto MapToHistoryDto(TaskHistoryApiDto dto) => new()
    {
        Id                = dto.Id,
        TaskId            = dto.TaskId,
        Action            = dto.Action,
        Details           = dto.Details,
        PerformedByUserId = dto.PerformedByUserId,
        CreatedAtUtc      = dto.CreatedAtUtc,
    };

    // ── Local wire types (match Task service camelCase JSON) ─────────────────────

    private sealed class TaskApiDto
    {
        public Guid      Id                      { get; set; }
        public Guid      TenantId                { get; set; }
        public string    Title                   { get; set; } = string.Empty;
        public string?   Description             { get; set; }
        public string    Status                  { get; set; } = string.Empty;
        public string    Priority                { get; set; } = string.Empty;
        public string    Scope                   { get; set; } = string.Empty;
        public Guid?     AssignedUserId          { get; set; }
        public string?   SourceProductCode       { get; set; }
        public string?   SourceEntityType        { get; set; }
        public Guid?     SourceEntityId          { get; set; }
        public Guid?     CurrentStageId          { get; set; }
        public Guid?     WorkflowInstanceId      { get; set; }
        public string?   WorkflowStepKey         { get; set; }
        public DateTime? DueAt                   { get; set; }
        public DateTime? CompletedAt             { get; set; }
        public Guid?     ClosedByUserId          { get; set; }
        public Guid?     CreatedByUserId         { get; set; }
        public Guid?     UpdatedByUserId         { get; set; }
        public DateTime  CreatedAtUtc            { get; set; }
        public DateTime  UpdatedAtUtc            { get; set; }
    }

    private sealed class TaskNoteApiDto
    {
        public Guid      Id              { get; set; }
        public Guid      TaskId          { get; set; }
        public string    Note            { get; set; } = string.Empty;
        public Guid?     CreatedByUserId { get; set; }
        public string?   AuthorName      { get; set; }
        public bool      IsEdited        { get; set; }
        public bool      IsDeleted       { get; set; }
        public DateTime  CreatedAtUtc    { get; set; }
    }

    private sealed class TaskHistoryApiDto
    {
        public Guid      Id                { get; set; }
        public Guid      TaskId            { get; set; }
        public string    Action            { get; set; } = string.Empty;
        public string?   Details           { get; set; }
        public Guid      PerformedByUserId { get; set; }
        public DateTime  CreatedAtUtc      { get; set; }
    }

    private sealed class TaskListApiDto
    {
        public List<TaskApiDto> Items    { get; set; } = [];
        public int              Total    { get; set; }
        public int              Page     { get; set; }
        public int              PageSize { get; set; }
    }
}
