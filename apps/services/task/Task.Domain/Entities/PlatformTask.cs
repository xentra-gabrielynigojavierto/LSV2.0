using BuildingBlocks.Domain;
using TaskStatus   = Task.Domain.Enums.TaskStatus;
using TaskPriority = Task.Domain.Enums.TaskPriority;
using TaskScope    = Task.Domain.Enums.TaskScope;

namespace Task.Domain.Entities;

public class PlatformTask : AuditableEntity
{
    public Guid    Id                 { get; private set; }
    public Guid    TenantId           { get; private set; }

    public string  Title              { get; private set; } = string.Empty;
    public string? Description        { get; private set; }

    public string  Status             { get; private set; } = TaskStatus.Open;
    public string  Priority           { get; private set; } = TaskPriority.Medium;

    public Guid?   AssignedUserId     { get; private set; }

    public string  Scope              { get; private set; } = TaskScope.General;
    public string? SourceProductCode    { get; private set; }
    public string? SourceEntityType    { get; private set; }
    public Guid?   SourceEntityId      { get; private set; }

    /// <summary>TASK-B04-01 — ID of the generation rule that created this task (null for manually created tasks).</summary>
    public Guid?   GenerationRuleId     { get; private set; }

    /// <summary>TASK-B04-01 — ID of the template that was applied when this task was auto-generated.</summary>
    public Guid?   GeneratingTemplateId { get; private set; }

    /// <summary>
    /// Optional reference to the current execution stage from <see cref="TaskStageConfig"/>.
    /// Null if no stage is assigned or stages are not configured for this tenant/product.
    /// </summary>
    public Guid?   CurrentStageId     { get; private set; }

    /// <summary>Optional reference to a Flow workflow instance that drives or owns this task.</summary>
    public Guid?   WorkflowInstanceId       { get; private set; }

    /// <summary>The current step key within the linked Flow workflow instance.</summary>
    public string? WorkflowStepKey          { get; private set; }

    /// <summary>Timestamp of the last meaningful change to workflow linkage fields.</summary>
    public DateTime? WorkflowLinkageChangedAt { get; private set; }

    public DateTime? DueAt            { get; private set; }
    public DateTime? CompletedAt      { get; private set; }
    public Guid?   ClosedByUserId     { get; private set; }

    // ── TASK-FLOW-02 — Flow queue assignment metadata ─────────────────────────
    /// <summary>
    /// Assignment mode from Flow: DirectUser, RoleQueue, OrgQueue, or Unassigned.
    /// Null for non-Flow tasks. Used to filter role/org queue reads.
    /// </summary>
    public string? AssignmentMode     { get; private set; }

    /// <summary>Role key that owns this task when <c>AssignmentMode = RoleQueue</c>.</summary>
    public string? AssignedRole       { get; private set; }

    /// <summary>Org ID that owns this task when <c>AssignmentMode = OrgQueue</c>.</summary>
    public string? AssignedOrgId      { get; private set; }

    /// <summary>UTC timestamp of the most recent assignment event. Null for Unassigned.</summary>
    public DateTime? AssignedAt       { get; private set; }

    /// <summary>
    /// String user ID (JWT sub) of the actor who performed the most recent assignment.
    /// Kept as string because Flow user IDs are strings; may or may not be a parseable Guid.
    /// </summary>
    public string? AssignedBy         { get; private set; }

    /// <summary>Free-form note recorded with the most recent assignment.</summary>
    public string? AssignmentReason   { get; private set; }

    // ── TASK-FLOW-02 — Additional lifecycle timestamps ────────────────────────
    /// <summary>UTC timestamp when the task moved to IN_PROGRESS. Null until started.</summary>
    public DateTime? StartedAt        { get; private set; }

    /// <summary>UTC timestamp when the task was cancelled. Null unless cancelled.</summary>
    public DateTime? CancelledAt      { get; private set; }

    // ── TASK-FLOW-02 — SLA state (pushed by Flow SLA evaluator) ──────────────
    /// <summary>
    /// SLA status: OnTrack, DueSoon, or Overdue.
    /// Populated by Flow's WorkflowTaskSlaEvaluator via the internal SLA push endpoint.
    /// Defaults to OnTrack for tasks with no DueAt or not yet evaluated.
    /// </summary>
    public string SlaStatus           { get; private set; } = "OnTrack";

    /// <summary>UTC timestamp of the first SLA breach observation. Null until Overdue.</summary>
    public DateTime? SlaBreachedAt    { get; private set; }

    /// <summary>UTC timestamp of the last SLA evaluation. Used to rotate the evaluator batch.</summary>
    public DateTime? LastSlaEvaluatedAt { get; private set; }

    private PlatformTask() { }

    public static PlatformTask Create(
        Guid      tenantId,
        string    title,
        Guid      createdByUserId,
        string?   description          = null,
        string?   priority             = null,
        string?   scope                = null,
        Guid?     assignedUserId       = null,
        string?   sourceProductCode    = null,
        string?   sourceEntityType     = null,
        Guid?     sourceEntityId       = null,
        DateTime? dueAt                = null,
        Guid?     currentStageId       = null,
        Guid?     workflowInstanceId   = null,
        string?   workflowStepKey      = null,
        Guid?     externalId           = null,
        Guid?     generationRuleId     = null,
        Guid?     generatingTemplateId = null,
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   assignedBy           = null,
        string?   assignmentReason     = null)
    {
        if (tenantId == Guid.Empty)        throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var effectivePriority = priority ?? TaskPriority.Medium;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        var effectiveScope = scope ?? TaskScope.General;
        if (!TaskScope.All.Contains(effectiveScope))
            throw new ArgumentException($"Invalid scope: '{effectiveScope}'.", nameof(scope));

        if (effectiveScope == TaskScope.Product && string.IsNullOrWhiteSpace(sourceProductCode))
            throw new ArgumentException("sourceProductCode is required for PRODUCT-scoped tasks.", nameof(sourceProductCode));

        var now = DateTime.UtcNow;
        return new PlatformTask
        {
            Id                    = externalId ?? Guid.NewGuid(),
            TenantId              = tenantId,
            Title                 = title.Trim(),
            Description           = description?.Trim(),
            Status                = TaskStatus.Open,
            Priority              = effectivePriority,
            Scope                 = effectiveScope,
            AssignedUserId        = assignedUserId,
            SourceProductCode     = sourceProductCode?.Trim().ToUpperInvariant(),
            SourceEntityType      = sourceEntityType?.Trim(),
            SourceEntityId        = sourceEntityId,
            GenerationRuleId      = generationRuleId,
            GeneratingTemplateId  = generatingTemplateId,
            DueAt                    = dueAt,
            CurrentStageId           = currentStageId,
            WorkflowInstanceId       = workflowInstanceId,
            WorkflowStepKey          = workflowStepKey?.Trim(),
            WorkflowLinkageChangedAt = workflowInstanceId.HasValue ? now : null,
            AssignmentMode           = assignmentMode,
            AssignedRole             = assignedRole,
            AssignedOrgId            = assignedOrgId,
            AssignedAt               = assignmentMode is not null ? now : null,
            AssignedBy               = assignedBy,
            AssignmentReason         = assignmentReason,
            SlaStatus                = "OnTrack",
            CreatedByUserId          = createdByUserId,
            UpdatedByUserId          = createdByUserId,
            CreatedAtUtc             = now,
            UpdatedAtUtc             = now,
        };
    }

    public void Update(
        string    title,
        Guid      updatedByUserId,
        string?   description    = null,
        string?   priority       = null,
        Guid?     assignedUserId = null,
        DateTime? dueAt          = null,
        Guid?     currentStageId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var effectivePriority = priority ?? Priority;
        if (!TaskPriority.All.Contains(effectivePriority))
            throw new ArgumentException($"Invalid priority: '{effectivePriority}'.", nameof(priority));

        Title           = title.Trim();
        Description     = description?.Trim();
        Priority        = effectivePriority;
        AssignedUserId  = assignedUserId;
        DueAt           = dueAt;
        CurrentStageId  = currentStageId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void TransitionStatus(string newStatus, Guid updatedByUserId)
    {
        if (!TaskStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid status: '{newStatus}'.", nameof(newStatus));
        if (TaskStatus.IsTerminal(Status))
            throw new InvalidOperationException($"Cannot transition from terminal status '{Status}'.");

        var now = DateTime.UtcNow;
        Status          = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = now;

        if (newStatus == TaskStatus.InProgress)
        {
            StartedAt = now;
        }
        else if (newStatus == TaskStatus.Completed)
        {
            CompletedAt    = now;
            ClosedByUserId = updatedByUserId;
        }
        else if (newStatus == TaskStatus.Cancelled)
        {
            CancelledAt    = now;
            ClosedByUserId = updatedByUserId;
        }
    }

    /// <summary>
    /// Assigns or unassigns the task. Returns the <see cref="AssignmentChangeKind"/>
    /// so the caller can write the appropriate history action (ASSIGNED / REASSIGNED / UNASSIGNED).
    /// Returns <see cref="AssignmentChangeKind.NoOp"/> when the requested value equals the current one.
    /// </summary>
    public AssignmentChangeKind Assign(Guid? userId, Guid updatedByUserId)
    {
        var previous = AssignedUserId;

        if (previous == userId)
            return AssignmentChangeKind.NoOp;

        AssignedUserId  = userId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;

        if (userId is null)
            return AssignmentChangeKind.Unassigned;

        return previous is null
            ? AssignmentChangeKind.Assigned
            : AssignmentChangeKind.Reassigned;
    }

    /// <summary>
    /// TASK-FLOW-02 — Sets queue assignment metadata from Flow's claim/reassign operations.
    /// Called via the internal flow-queue-assign endpoint.
    /// </summary>
    public void SetFlowQueueAssignment(
        string?  assignmentMode,
        Guid?    assignedUserId,
        string?  assignedRole,
        string?  assignedOrgId,
        string?  assignedBy,
        string?  assignmentReason)
    {
        var now         = DateTime.UtcNow;
        AssignmentMode  = assignmentMode;
        AssignedUserId  = assignedUserId;
        AssignedRole    = assignedRole;
        AssignedOrgId   = assignedOrgId;
        AssignedAt      = now;
        AssignedBy      = assignedBy;
        AssignmentReason = assignmentReason;
        UpdatedAtUtc    = now;
    }

    /// <summary>
    /// TASK-FLOW-02 — Updates SLA state pushed from Flow's WorkflowTaskSlaEvaluator.
    /// </summary>
    public void SetSlaState(string slaStatus, DateTime? slaBreachedAt, DateTime evaluatedAt)
    {
        SlaStatus           = slaStatus;
        SlaBreachedAt       = slaBreachedAt;
        LastSlaEvaluatedAt  = evaluatedAt;
        UpdatedAtUtc        = evaluatedAt;
    }

    /// <summary>Sets or clears the current execution stage.</summary>
    public void SetStage(Guid? stageId, Guid updatedByUserId)
    {
        CurrentStageId  = stageId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates Flow workflow linkage fields. Returns <c>true</c> if any field changed
    /// (for idempotency — callers can skip writing history when this returns false).
    /// </summary>
    public bool SetWorkflowLinkage(Guid? workflowInstanceId, string? stepKey, Guid updatedByUserId)
    {
        var normalizedStep = stepKey?.Trim();

        if (WorkflowInstanceId == workflowInstanceId && WorkflowStepKey == normalizedStep)
            return false;

        WorkflowInstanceId        = workflowInstanceId;
        WorkflowStepKey           = normalizedStep;
        WorkflowLinkageChangedAt  = DateTime.UtcNow;
        UpdatedByUserId           = updatedByUserId;
        UpdatedAtUtc              = DateTime.UtcNow;
        return true;
    }
}

/// <summary>Describes what kind of assignment change occurred.</summary>
public enum AssignmentChangeKind
{
    NoOp       = 0,
    Assigned   = 1,
    Reassigned = 2,
    Unassigned = 3,
}
