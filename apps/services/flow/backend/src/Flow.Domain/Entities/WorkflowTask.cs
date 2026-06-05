using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-E11.1 — first-class human-work item produced by workflow
/// execution. Distinct from the legacy <see cref="TaskItem"/> grain
/// (which lives at the *definition* layer and predates the dedicated
/// <see cref="WorkflowInstance"/> introduced in MERGE-P4).
///
/// <para>
/// <b>Layering:</b> WorkflowTask is the work-item layer; <see cref="WorkflowInstance"/>
/// remains the sole execution authority. This phase does NOT add any
/// engine wiring — no automatic creation on advance, no progression
/// binding, no APIs. Its only job here is to exist as a durable,
/// tenant-scoped, queryable surface that later phases (E11.2+) can
/// build creation/assignment/progression behaviour on top of.
/// </para>
///
/// <para>
/// <b>Linkage:</b> always points at a <see cref="WorkflowInstance"/>
/// (required) and the workflow <c>StepKey</c> it was raised against
/// (required, mirrors <see cref="WorkflowInstance.CurrentStepKey"/> /
/// <see cref="WorkflowStage.Key"/>). Multiple tasks per instance over
/// time are explicitly supported — no uniqueness constraint on
/// <c>(WorkflowInstanceId, StepKey)</c>.
/// </para>
///
/// <para>
/// <b>Tenant scoping:</b> <c>TenantId</c> (inherited from
/// <see cref="Common.BaseEntity"/>) is required and enforced by both
/// the <see cref="Infrastructure.Persistence.FlowDbContext"/> save hook
/// and a query filter on the entity, identical to every other Flow
/// grain.
/// </para>
///
/// <para>
/// <b>Domain invariants enforced (E11.1 + E14.1):</b>
///   <list type="bullet">
///     <item><see cref="WorkflowInstanceId"/>, <see cref="StepKey"/>, <see cref="Status"/>, <c>TenantId</c> are required.</item>
///     <item>If <see cref="CompletedAt"/> is set, <see cref="Status"/> must be <see cref="WorkflowTaskStatus.Completed"/>.</item>
///     <item>If <see cref="CancelledAt"/> is set, <see cref="Status"/> must be <see cref="WorkflowTaskStatus.Cancelled"/>.</item>
///     <item>(E14.1) <see cref="AssignmentMode"/> is required and must
///       be one of <see cref="WorkflowTaskAssignmentMode"/>; the
///       (user, role, org) triple is governed by the mode and only ONE
///       mode is valid at a time.</item>
///   </list>
/// </para>
/// </summary>
public class WorkflowTask : AuditableEntity
{
    // ---------------- Linkage to execution layer -----------------

    /// <summary>Owning workflow instance (required).</summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Workflow step the task was raised for. Stable string key that
    /// mirrors <see cref="WorkflowStage.Key"/> /
    /// <see cref="WorkflowInstance.CurrentStepKey"/>. Stored as a
    /// string (not an FK to <see cref="WorkflowStage"/>) so a task
    /// survives definition edits and so cross-version comparisons
    /// remain straightforward.
    /// </summary>
    public string StepKey { get; set; } = string.Empty;

    // ---------------- Descriptive payload -----------------

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // ---------------- Lifecycle state -----------------

    /// <summary>One of <see cref="WorkflowTaskStatus"/>. Defaults to <c>Open</c>.</summary>
    public string Status { get; set; } = WorkflowTaskStatus.Open;

    /// <summary>One of <see cref="WorkflowTaskPriority"/>. Defaults to <c>Normal</c>.</summary>
    public string Priority { get; set; } = WorkflowTaskPriority.Normal;

    // ---------------- Assignment (E14.1) -----------------
    //
    // The (user, role, org) triple is now governed by AssignmentMode.
    // EnsureValid() enforces a single mode per row and rejects every
    // other combination so the column shape can never be ambiguous.

    /// <summary>
    /// One of <see cref="WorkflowTaskAssignmentMode"/>. Defaults to
    /// <see cref="WorkflowTaskAssignmentMode.Unassigned"/>. Required
    /// at persistence time — see <see cref="EnsureValid"/>. Producers
    /// that leave it empty get a deterministic derivation from the
    /// (user, role, org) values via <see cref="NormalizeAssignmentMode"/>.
    /// </summary>
    public string AssignmentMode { get; set; } = WorkflowTaskAssignmentMode.Unassigned;

    public string? AssignedUserId { get; set; }
    public string? AssignedRole   { get; set; }
    public string? AssignedOrgId  { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent assignment event. Stamped by
    /// the producer (engine factory in E14.1; claim / reassign service
    /// in E14.2). Must be null when <see cref="AssignmentMode"/> is
    /// <see cref="WorkflowTaskAssignmentMode.Unassigned"/>.
    /// </summary>
    public DateTime? AssignedAt { get; set; }

    /// <summary>
    /// Identifier of the actor responsible for the most recent
    /// assignment event. Null for system / resolver-routed
    /// assignments. Must be null when <see cref="AssignmentMode"/> is
    /// <see cref="WorkflowTaskAssignmentMode.Unassigned"/>.
    /// </summary>
    public string? AssignedBy { get; set; }

    /// <summary>
    /// Optional free-form note describing why the current assignment
    /// was applied (e.g. "claimed from queue", "reassigned by
    /// supervisor"). Surfaced for audit only — engine logic does not
    /// branch on it.
    /// </summary>
    public string? AssignmentReason { get; set; }

    // ---------------- Lifecycle timestamps -----------------
    //
    // CreatedAt / UpdatedAt / CreatedBy / UpdatedBy come from
    // AuditableEntity. Started/Completed/Cancelled are domain-specific
    // and only set by the (future) lifecycle handlers — left null on
    // construction.

    public DateTime? StartedAt   { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // ---------------- SLA / Timer (LS-FLOW-E10.3) -----------------
    //
    // Additive time-awareness layer. DueAt is stamped exactly once at
    // creation by WorkflowTaskFromWorkflowFactory using the per-priority
    // duration from WorkflowTaskSlaOptions; reassignment / claim do NOT
    // recompute it. SlaStatus is owned by WorkflowTaskSlaEvaluator,
    // which is the only writer of SlaStatus / SlaBreachedAt /
    // LastSlaEvaluatedAt after creation.

    /// <summary>
    /// UTC deadline for this task. Null when no SLA was applicable at
    /// creation time (e.g. legacy rows pre-E10.3 phase 2). The
    /// evaluator skips rows where this is null.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// One of <see cref="WorkflowSlaStatus"/> (OnTrack / DueSoon /
    /// Overdue). Defaults to <c>OnTrack</c> so a freshly stamped task
    /// with a future <see cref="DueAt"/> reads correctly even before
    /// the evaluator's first visit. The evaluator never persists
    /// <c>Escalated</c> for tasks in this phase.
    /// </summary>
    public string SlaStatus { get; set; } = WorkflowSlaStatus.OnTrack;

    /// <summary>
    /// First-observation breach timestamp. Stamped by the evaluator on
    /// the visit that first classifies the row as Overdue, then never
    /// rewritten (so the row carries a stable breach marker even if it
    /// later flips back to OnTrack via an admin-set DueAt change).
    /// </summary>
    public DateTime? SlaBreachedAt { get; set; }

    /// <summary>
    /// Stamped by the evaluator on every visit, regardless of whether
    /// the SLA state changed. Drives the evaluator's fair-rotation
    /// ordering (oldest evaluated first).
    /// </summary>
    public DateTime? LastSlaEvaluatedAt { get; set; }

    /// <summary>
    /// Reserved for the future SLA designer phase. Null in this phase
    /// — duration is read from <c>WorkflowTaskSla:Durations</c> by
    /// priority. Persisting the column now keeps the schema forward-
    /// compatible without an additional migration later.
    /// </summary>
    public string? SlaPolicyKey { get; set; }

    // ---------------- Extensibility surfaces -----------------

    /// <summary>
    /// Free-form correlation key (external case number, ticket id,
    /// idempotency key, …). Indexed at the <c>(TenantId, …)</c> level
    /// in a future phase if query patterns demand it; for now it is a
    /// plain searchable column.
    /// </summary>
    public string? CorrelationKey { get; set; }

    /// <summary>
    /// Opaque JSON metadata bag. Kept as <c>longtext</c>/string to
    /// avoid imposing a schema before product use cases stabilise.
    /// Consumers should treat unknown keys as forward-compatible.
    /// </summary>
    public string? MetadataJson { get; set; }

    // ---------------- Navigation -----------------

    /// <summary>The owning workflow instance. Restrict-on-delete at the FK.</summary>
    public WorkflowInstance? WorkflowInstance { get; set; }

    // ---------------- Domain invariants -----------------

    /// <summary>
    /// LS-FLOW-E14.1 — backward-compat backstop. Derives
    /// <see cref="AssignmentMode"/> from the current (user, role, org)
    /// triple using the same precedence as the E11.3
    /// <see cref="WorkflowTaskAssignment"/> factory whenever the row is
    /// <em>not in a state a caller could have set on purpose</em>:
    ///   <list type="bullet">
    ///     <item>mode is null or whitespace (legacy hand-constructed
    ///       row), or</item>
    ///     <item>mode is the entity's default
    ///       <see cref="WorkflowTaskAssignmentMode.Unassigned"/> but at
    ///       least one assignment target is populated (legacy producer
    ///       that filled targets and ignored the new mode field — the
    ///       common shape for E11.3 code paths that have not yet
    ///       picked up the E14.1 wiring).</item>
    ///   </list>
    /// The "Unassigned + no targets" case is left alone — it is the
    /// genuine no-assignment shape and re-deriving would be a no-op.
    /// Idempotent; safe to call repeatedly.
    /// </summary>
    public void NormalizeAssignmentMode()
    {
        var hasAnyTarget =
            !string.IsNullOrWhiteSpace(AssignedUserId) ||
            !string.IsNullOrWhiteSpace(AssignedRole) ||
            !string.IsNullOrWhiteSpace(AssignedOrgId);

        var modeIsBlank = string.IsNullOrWhiteSpace(AssignmentMode);
        var modeIsDefaultButTargetsPresent =
            string.Equals(AssignmentMode, WorkflowTaskAssignmentMode.Unassigned, StringComparison.Ordinal)
            && hasAnyTarget;

        if (modeIsBlank || modeIsDefaultButTargetsPresent)
        {
            AssignmentMode = WorkflowTaskAssignmentMode.Derive(AssignedUserId, AssignedRole, AssignedOrgId);
        }
    }

    /// <summary>
    /// Validates the minimal invariants documented on this type. Called
    /// from <see cref="Infrastructure.Persistence.FlowDbContext.SaveChangesAsync"/>
    /// for added / modified rows; throws <see cref="InvalidOperationException"/>
    /// on violation so the writer fails loudly rather than persisting an
    /// internally inconsistent task.
    /// </summary>
    public void EnsureValid()
    {
        if (WorkflowInstanceId == Guid.Empty)
            throw new InvalidOperationException("WorkflowTask.WorkflowInstanceId is required.");
        if (string.IsNullOrWhiteSpace(StepKey))
            throw new InvalidOperationException("WorkflowTask.StepKey is required.");
        if (string.IsNullOrWhiteSpace(Status))
            throw new InvalidOperationException("WorkflowTask.Status is required.");

        // Terminal status ↔ timestamp pairing is enforced in BOTH
        // directions so an internally inconsistent row (e.g. Status=
        // Completed with no CompletedAt, or CompletedAt set on a
        // non-terminal status) cannot be persisted. Spec calls out the
        // forward direction explicitly; the reverse follows from the
        // same invariant and is closed off here defensively.
        var isCompleted = string.Equals(Status, WorkflowTaskStatus.Completed, StringComparison.Ordinal);
        var isCancelled = string.Equals(Status, WorkflowTaskStatus.Cancelled, StringComparison.Ordinal);

        if (CompletedAt is not null && !isCompleted)
            throw new InvalidOperationException(
                $"WorkflowTask.CompletedAt is set but Status='{Status}' (expected '{WorkflowTaskStatus.Completed}').");
        if (isCompleted && CompletedAt is null)
            throw new InvalidOperationException(
                $"WorkflowTask.Status='{WorkflowTaskStatus.Completed}' requires CompletedAt to be set.");

        if (CancelledAt is not null && !isCancelled)
            throw new InvalidOperationException(
                $"WorkflowTask.CancelledAt is set but Status='{Status}' (expected '{WorkflowTaskStatus.Cancelled}').");
        if (isCancelled && CancelledAt is null)
            throw new InvalidOperationException(
                $"WorkflowTask.Status='{WorkflowTaskStatus.Cancelled}' requires CancelledAt to be set.");

        // ---------------- E14.1 — assignment-model invariants ----------------
        //
        // Lazy backward-compat mapping FIRST so a row written by older
        // code (or by a test that constructed a WorkflowTask by hand)
        // is normalised before we reject it. After normalisation the
        // mode is non-empty for every legal combination.
        NormalizeAssignmentMode();

        if (!WorkflowTaskAssignmentMode.IsKnown(AssignmentMode))
            throw new InvalidOperationException(
                $"WorkflowTask.AssignmentMode='{AssignmentMode}' is not a known mode. " +
                $"Allowed: DirectUser, RoleQueue, OrgQueue, Unassigned.");

        switch (AssignmentMode)
        {
            case WorkflowTaskAssignmentMode.DirectUser:
                if (string.IsNullOrWhiteSpace(AssignedUserId))
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='DirectUser' requires AssignedUserId.");
                if (AssignedRole is not null)
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='DirectUser' forbids AssignedRole.");
                if (AssignedOrgId is not null)
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='DirectUser' forbids AssignedOrgId.");
                break;

            case WorkflowTaskAssignmentMode.RoleQueue:
                if (string.IsNullOrWhiteSpace(AssignedRole))
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='RoleQueue' requires AssignedRole.");
                if (AssignedUserId is not null)
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='RoleQueue' forbids AssignedUserId.");
                break;

            case WorkflowTaskAssignmentMode.OrgQueue:
                if (string.IsNullOrWhiteSpace(AssignedOrgId))
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='OrgQueue' requires AssignedOrgId.");
                if (AssignedUserId is not null)
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='OrgQueue' forbids AssignedUserId.");
                break;

            case WorkflowTaskAssignmentMode.Unassigned:
                if (AssignedUserId is not null || AssignedRole is not null || AssignedOrgId is not null)
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='Unassigned' forbids AssignedUserId / AssignedRole / AssignedOrgId.");
                if (AssignedAt is not null || !string.IsNullOrWhiteSpace(AssignedBy))
                    throw new InvalidOperationException(
                        "WorkflowTask.AssignmentMode='Unassigned' forbids AssignedAt / AssignedBy (no assignment event has occurred).");
                break;
        }
    }
}
