using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskRequest(
    string    Title,
    string?   Description          = null,
    string?   Priority             = null,
    string?   Scope                = null,
    Guid?     AssignedUserId       = null,
    string?   SourceProductCode    = null,
    string?   SourceEntityType     = null,
    Guid?     SourceEntityId       = null,
    DateTime? DueAt                = null,
    Guid?     WorkflowInstanceId   = null,
    string?   WorkflowStepKey      = null,
    /// <summary>
    /// TASK-B04 — Optional client-supplied ID. When provided the task is created with this ID
    /// instead of a server-generated one. Used by Liens consumer cutover so that Liens task IDs
    /// and canonical Task service IDs are the same Guid (no cross-reference table needed).
    /// </summary>
    Guid?     ExternalId           = null,
    /// <summary>TASK-B04-01 — ID of the generation rule that triggered auto-creation (null for manual tasks).</summary>
    Guid?     GenerationRuleId     = null,
    /// <summary>TASK-B04-01 — ID of the template applied during auto-generation.</summary>
    Guid?     GeneratingTemplateId = null,
    /// <summary>TASK-FLOW-02 — Flow assignment mode: DirectUser, RoleQueue, OrgQueue, or Unassigned.</summary>
    string?   AssignmentMode       = null,
    /// <summary>TASK-FLOW-02 — Role key when AssignmentMode = RoleQueue.</summary>
    string?   AssignedRole         = null,
    /// <summary>TASK-FLOW-02 — Org ID when AssignmentMode = OrgQueue.</summary>
    string?   AssignedOrgId        = null,
    /// <summary>TASK-FLOW-02 — String user ID (JWT sub) of the actor who set the initial assignment.</summary>
    string?   AssignedBy           = null,
    /// <summary>TASK-FLOW-02 — Free-form note recorded with the initial assignment.</summary>
    string?   AssignmentReason     = null);

public record UpdateTaskRequest(
    string    Title,
    string?   Description    = null,
    string?   Priority       = null,
    Guid?     AssignedUserId = null,
    DateTime? DueAt          = null);

public record AssignTaskRequest(Guid? AssignedUserId);

public record TransitionStatusRequest(string Status);

/// <param name="AuthorName">Display name of the author (optional). Used by consumer products that track user display names.</param>
public record AddNoteRequest(string Note, string? AuthorName = null);

/// <summary>Full task projection returned by all task endpoints.</summary>
public record TaskDto(
    Guid      Id,
    Guid      TenantId,
    string    Title,
    string?   Description,
    string    Status,
    string    Priority,
    string    Scope,
    Guid?     AssignedUserId,
    string?   SourceProductCode,
    string?   SourceEntityType,
    Guid?     SourceEntityId,
    Guid?     CurrentStageId,
    Guid?     WorkflowInstanceId,
    string?   WorkflowStepKey,
    DateTime? WorkflowLinkageChangedAt,
    DateTime? DueAt,
    DateTime? CompletedAt,
    Guid?     ClosedByUserId,
    Guid?     CreatedByUserId,
    Guid?     UpdatedByUserId,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc,
    // TASK-FLOW-02 — Flow queue assignment metadata
    string?   AssignmentMode     = null,
    string?   AssignedRole       = null,
    string?   AssignedOrgId      = null,
    DateTime? AssignedAt         = null,
    string?   AssignedBy         = null,
    string?   AssignmentReason   = null,
    // TASK-FLOW-02 — Additional lifecycle timestamps
    DateTime? StartedAt          = null,
    DateTime? CancelledAt        = null,
    // TASK-FLOW-02 — SLA state
    string    SlaStatus          = "OnTrack",
    DateTime? SlaBreachedAt      = null,
    DateTime? LastSlaEvaluatedAt = null)
{
    public static TaskDto From(PlatformTask t) => new(
        t.Id, t.TenantId, t.Title, t.Description,
        t.Status, t.Priority, t.Scope,
        t.AssignedUserId,
        t.SourceProductCode, t.SourceEntityType, t.SourceEntityId,
        t.CurrentStageId,
        t.WorkflowInstanceId, t.WorkflowStepKey, t.WorkflowLinkageChangedAt,
        t.DueAt, t.CompletedAt, t.ClosedByUserId,
        t.CreatedByUserId, t.UpdatedByUserId,
        t.CreatedAtUtc, t.UpdatedAtUtc,
        AssignmentMode:     t.AssignmentMode,
        AssignedRole:       t.AssignedRole,
        AssignedOrgId:      t.AssignedOrgId,
        AssignedAt:         t.AssignedAt,
        AssignedBy:         t.AssignedBy,
        AssignmentReason:   t.AssignmentReason,
        StartedAt:          t.StartedAt,
        CancelledAt:        t.CancelledAt,
        SlaStatus:          t.SlaStatus,
        SlaBreachedAt:      t.SlaBreachedAt,
        LastSlaEvaluatedAt: t.LastSlaEvaluatedAt);
}

public record TaskNoteDto(
    Guid     Id,
    Guid     TaskId,
    string   Note,
    Guid?    CreatedByUserId,
    string?  AuthorName,
    bool     IsEdited,
    bool     IsDeleted,
    DateTime CreatedAtUtc)
{
    public static TaskNoteDto From(TaskNote n) => new(
        n.Id, n.TaskId, n.Note, n.CreatedByUserId, n.AuthorName, n.IsEdited, n.IsDeleted, n.CreatedAtUtc);
}

public record TaskHistoryDto(
    Guid     Id,
    Guid     TaskId,
    string   Action,
    string?  Details,
    Guid     PerformedByUserId,
    DateTime CreatedAtUtc)
{
    public static TaskHistoryDto From(TaskHistory h) => new(
        h.Id, h.TaskId, h.Action, h.Details, h.PerformedByUserId, h.CreatedAtUtc);
}

public record TaskListResponse(
    IReadOnlyList<TaskDto> Items,
    int                    Total,
    int                    Page,
    int                    PageSize);

/// <summary>Request to update Flow workflow linkage on a task (admin only).</summary>
public record UpdateWorkflowLinkageRequest(
    Guid?   WorkflowInstanceId,
    string? WorkflowStepKey);

/// <summary>
/// Callback payload sent from Flow (or a background orchestrator) when a workflow step transitions.
/// Identifies all tasks linked to the given WorkflowInstanceId and updates their WorkflowStepKey.
/// Idempotent: no-op when the step key is already set to NewStepKey.
/// </summary>
public record FlowStepCallbackRequest(
    Guid    WorkflowInstanceId,
    string  NewStepKey,
    Guid    TenantId,
    Guid?   UpdatedByUserId = null);

/// <summary>Result returned from the flow-callback endpoint.</summary>
public record FlowCallbackResult(
    int    TasksUpdated,
    int    TasksSkipped,
    Guid   WorkflowInstanceId,
    string NewStepKey);

/// <summary>Lightweight workflow-context projection for a task.</summary>
public record TaskWorkflowContextDto(
    Guid      TaskId,
    Guid?     WorkflowInstanceId,
    string?   WorkflowStepKey,
    DateTime? WorkflowLinkageChangedAt);

/// <summary>Task count summary grouped by product code and status.</summary>
public record TaskProductSummaryDto(
    string? ProductCode,
    string  Status,
    int     Count);

/// <summary>Result of GET /api/tasks/my/summary — cross-product task counts for the current user.</summary>
public record MyTaskSummaryResponse(
    IReadOnlyList<TaskProductSummaryDto> Summary,
    int                                  TotalOpen,
    int                                  TotalOverdue);

/// <summary>Linked entity DTO returned from linked-entity endpoints.</summary>
public record TaskLinkedEntityDto(
    Guid     Id,
    Guid     TaskId,
    string?  SourceProductCode,
    string   EntityType,
    string   EntityId,
    string   RelationshipType,
    DateTime CreatedAtUtc)
{
    public static TaskLinkedEntityDto From(TaskLinkedEntity e) => new(
        e.Id, e.TaskId, e.SourceProductCode, e.EntityType,
        e.EntityId, e.RelationshipType, e.CreatedAtUtc);
}

/// <summary>Request to add a linked entity to a task.</summary>
public record AddLinkedEntityRequest(
    string  EntityType,
    string  EntityId,
    string  RelationshipType  = "RELATED",
    string? SourceProductCode = null);

// ── TASK-FLOW-02 — Internal Flow integration DTOs ──────────────────────────

/// <summary>
/// TASK-FLOW-02 — Payload for a single task's SLA state update.
/// Sent by Flow's WorkflowTaskSlaEvaluator to the internal SLA push endpoint.
/// </summary>
public record FlowSlaUpdateItem(
    Guid      TaskId,
    string    SlaStatus,
    DateTime? SlaBreachedAt,
    DateTime  EvaluatedAt);

/// <summary>
/// TASK-FLOW-02 — Batch SLA update request sent by Flow's SLA evaluator.
/// </summary>
public record FlowSlaUpdateRequest(IReadOnlyList<FlowSlaUpdateItem> Updates);

/// <summary>Result of the internal SLA update batch.</summary>
public record FlowSlaUpdateResult(int Updated, int NotFound);

/// <summary>
/// TASK-FLOW-02 — Sets Flow queue assignment metadata on a task via the internal endpoint.
/// Called on claim/reassign operations delegated by Flow.
/// </summary>
public record FlowQueueAssignRequest(
    string?  AssignmentMode,
    Guid?    AssignedUserId,
    string?  AssignedRole,
    string?  AssignedOrgId,
    string?  AssignedBy,
    string?  AssignmentReason);

/// <summary>Result of the internal queue assignment update.</summary>
public record FlowQueueAssignResult(bool Updated, string? Error = null);

// ── TASK-FLOW-03 — SLA batch evaluation read ──────────────────────────────

/// <summary>
/// TASK-FLOW-03 — Single task projection returned by the internal
/// SLA batch evaluation endpoint. Contains only the fields needed by
/// Flow's <c>WorkflowTaskSlaEvaluator</c> to compute SLA transitions.
/// </summary>
public record FlowSlaBatchItem(
    Guid      TaskId,
    Guid      TenantId,
    DateTime? DueAt,
    string    SlaStatus,
    DateTime? SlaBreachedAt);

/// <summary>
/// TASK-FLOW-03 — Response wrapper for the internal SLA batch read.
/// </summary>
public record FlowSlaBatchResponse(IReadOnlyList<FlowSlaBatchItem> Items);
