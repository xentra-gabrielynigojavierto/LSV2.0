using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

/// <summary>
/// TASK-B04 / TASK-010 — HTTP client interface for the canonical Task service.
/// Liens is a consumer-only of this client; it does NOT own task runtime.
/// All methods map Liens domain concepts onto Task service API contracts.
/// </summary>
public interface ILiensTaskServiceClient
{
    Task<TaskResponse> CreateTaskAsync(
        Guid   tenantId,
        Guid   actingUserId,
        Guid   externalId,
        CreateTaskRequest request,
        CancellationToken ct = default);

    Task<TaskResponse?> GetTaskAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    Task<PaginatedResult<TaskResponse>> SearchTasksAsync(
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
        CancellationToken ct = default);

    Task<TaskResponse> UpdateTaskAsync(
        Guid   tenantId,
        Guid   id,
        Guid   actingUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default);

    Task<TaskResponse> AssignTaskAsync(
        Guid  tenantId,
        Guid  id,
        Guid  actingUserId,
        Guid? assignedUserId,
        CancellationToken ct = default);

    Task<TaskResponse> TransitionStatusAsync(
        Guid   tenantId,
        Guid   id,
        Guid   actingUserId,
        string newStatus,
        CancellationToken ct = default);

    Task<TaskNoteResponse> AddNoteAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   actorUserId,
        string content,
        string createdByName,
        CancellationToken ct = default);

    Task<List<TaskNoteResponse>> GetNotesAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default);

    Task<TaskNoteResponse> UpdateNoteAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   noteId,
        Guid   actorUserId,
        string content,
        CancellationToken ct = default);

    Task DeleteNoteAsync(
        Guid tenantId,
        Guid taskId,
        Guid noteId,
        Guid actorUserId,
        CancellationToken ct = default);

    Task AddLinkedEntityAsync(
        Guid   tenantId,
        Guid   taskId,
        Guid   actingUserId,
        string entityType,
        Guid   entityId,
        string relationship,
        CancellationToken ct = default);

    Task TriggerFlowCallbackAsync(
        Guid   tenantId,
        Guid   workflowInstanceId,
        string newStepKey,
        CancellationToken ct = default);

    Task<BackfillTaskResult> BackfillTaskAsync(
        Guid   tenantId,
        Guid   actingUserId,
        Guid   externalId,
        CreateTaskRequest request,
        IReadOnlyList<(Guid NoteId, string Content, string AuthorName, Guid AuthorId, DateTime CreatedAt)> notes,
        IReadOnlyList<Guid> lienIds,
        CancellationToken ct = default);

    // ── TASK-MIG-01 — Governance settings round-trip with Task service ───────────

    /// <summary>
    /// Fetches governance settings for the given tenant and product from the Task service.
    /// Returns null when no settings exist yet (HTTP 204 No Content).
    /// </summary>
    Task<TaskServiceGovernanceResponse?> GetGovernanceAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates governance settings in the Task service.
    /// Used to keep the Task service in sync after a Liens-side governance update.
    /// </summary>
    System.Threading.Tasks.Task UpsertGovernanceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceGovernanceUpsertRequest payload,
        CancellationToken ct = default);

    // ── TASK-MIG-02 — Template round-trip with Task service ──────────────────────

    /// <summary>
    /// Fetches a single template from the Task service by ID.
    /// Returns null if not found (HTTP 204/404).
    /// </summary>
    Task<TaskServiceTemplateResponse?> GetTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all templates for the given tenant and product from the Task service.
    /// Returns an empty list if none exist.
    /// </summary>
    Task<List<TaskServiceTemplateResponse>> GetAllTemplatesAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a template in the Task service using the source-product upsert endpoint.
    /// Used by LiensTemplateSyncService and write-through on admin saves.
    /// </summary>
    System.Threading.Tasks.Task UpsertTemplateFromSourceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceTemplateUpsertRequest payload,
        CancellationToken ct = default);

    // ── TASK-MIG-03 — Stage config round-trip with Task service ─────────────────

    /// <summary>
    /// Fetches a single stage config from the Task service by ID.
    /// Returns null if not found (HTTP 404).
    /// </summary>
    Task<TaskServiceStageResponse?> GetStageAsync(
        Guid tenantId,
        Guid stageId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all stage configs for the given tenant and product from the Task service.
    /// Returns an empty list if none exist.
    /// </summary>
    Task<List<TaskServiceStageResponse>> GetAllStagesAsync(
        Guid   tenantId,
        string productCode,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a stage config in the Task service via the from-source endpoint.
    /// Used by LiensStageSyncService and write-through on admin saves.
    /// </summary>
    System.Threading.Tasks.Task UpsertStageFromSourceAsync(
        Guid   tenantId,
        Guid   actingUserId,
        TaskServiceStageUpsertRequest payload,
        CancellationToken ct = default);

    // ── TASK-B04-01 — Duplicate-prevention helpers for LienTaskGenerationEngine ──

    /// <summary>
    /// Returns true if an active (non-terminal) task already exists in the Task service
    /// that was generated by the given rule for the given case or lien entity.
    /// Used by <see cref="LienTaskGenerationEngine"/> to prevent duplicate auto-generation.
    /// </summary>
    Task<bool> HasOpenTaskForRuleAsync(
        Guid  tenantId,
        Guid  ruleId,
        Guid? caseId,
        Guid? lienId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if an active (non-terminal) task already exists in the Task service
    /// that was generated from the given template for the given case or lien entity.
    /// Used by <see cref="LienTaskGenerationEngine"/> to prevent duplicate auto-generation.
    /// </summary>
    Task<bool> HasOpenTaskForTemplateAsync(
        Guid  tenantId,
        Guid  templateId,
        Guid? caseId,
        Guid? lienId,
        CancellationToken ct = default);

    // ── TASK-MIG-04 — Transition round-trip ──────────────────────────────────────

    /// <summary>Returns all active transitions for (tenantId, productCode) from Task service.</summary>
    Task<List<TaskServiceTransitionResponse>> GetTransitionsAsync(
        Guid tenantId, string productCode, CancellationToken ct = default);

    /// <summary>Batch-replaces transitions for (tenantId, productCode) in Task service (idempotent).</summary>
    System.Threading.Tasks.Task UpsertTransitionsFromSourceAsync(
        Guid tenantId, Guid actorId, TaskServiceTransitionsUpsertRequest request, CancellationToken ct = default);

    /// <summary>Returns the ordered change history for a single task from the Task service.</summary>
    Task<List<TaskHistoryEventDto>> GetHistoryAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken ct = default);
}

public sealed record BackfillTaskResult(Guid TaskId, int NotesCreated, int LinksCreated, bool AlreadyExisted);
