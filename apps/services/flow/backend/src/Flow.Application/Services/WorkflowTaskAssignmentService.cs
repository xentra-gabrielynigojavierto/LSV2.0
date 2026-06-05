using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E14.2 — sole entry point for user-driven assignment
/// transitions on <see cref="WorkflowTask"/>. Implements the
/// claim/reassign governance described in the E14.2 spec.
///
/// <para>
/// <b>TASK-FLOW-03 (post-migration):</b> the shadow table
/// (<c>flow_workflow_tasks</c>) has been dropped.
/// <c>ReadSnapshotAsync</c> now delegates to
/// <see cref="IFlowTaskServiceClient.GetTaskByIdAsync"/> (Task service
/// as read authority). The shadow <c>ExecuteUpdateAsync</c> CAS that
/// previously followed the Task service write has been removed.
/// Consistency is owned entirely by the Task service —
/// <see cref="IFlowTaskServiceClient.SetQueueAssignmentAsync"/> is the
/// sole write. The status read in the snapshot is normalised from Task
/// service UPPERCASE to Flow PascalCase so all downstream comparisons
/// against <see cref="WorkflowTaskStatus"/> continue to work unchanged.
/// </para>
///
/// <para>
/// <b>Audit.</b> Best-effort emission of
/// <c>workflow.task.claim</c> / <c>workflow.task.reassign</c> via
/// <see cref="IAuditAdapter"/>. Per the adapter's "fire-and-forget
/// safe" contract, audit failures are logged and swallowed so the
/// user-visible operation is not undone by an audit-pipeline outage.
/// </para>
/// </summary>
public sealed class WorkflowTaskAssignmentService : IWorkflowTaskAssignmentService
{
    private const int MaxReasonLength = 500;
    private const string DefaultClaimReason = "claimed from queue";

    private const string RolePlatformAdmin = "PlatformAdmin";
    private const string RoleTenantAdmin   = "TenantAdmin";

    private readonly IFlowUserContext _user;
    private readonly IAuditAdapter _audit;
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly ILogger<WorkflowTaskAssignmentService> _log;

    public WorkflowTaskAssignmentService(
        IFlowUserContext user,
        IAuditAdapter audit,
        IFlowTaskServiceClient taskClient,
        ILogger<WorkflowTaskAssignmentService> log)
    {
        _user       = user;
        _audit      = audit;
        _taskClient = taskClient;
        _log        = log;
    }

    // ====================== CLAIM ======================

    public async Task<WorkflowTaskAssignmentResult> ClaimAsync(
        Guid taskId,
        string? reason,
        CancellationToken ct = default)
    {
        var callerUserId = _user.UserId
            ?? throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Claim requires an authenticated caller.");

        var snapshot = await ReadSnapshotAsync(taskId, ct);

        // 1. State guard — only Open is claimable. InProgress means
        //    someone has already started; use reassign for redirect.
        if (!string.Equals(snapshot.Status, WorkflowTaskStatus.Open, StringComparison.Ordinal))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskStateInvalid,
                $"Task status '{snapshot.Status}' is not claimable. Only 'Open' tasks can be claimed.");
        }

        // 2. Source-mode guard.
        switch (snapshot.AssignmentMode)
        {
            case WorkflowTaskAssignmentMode.DirectUser:
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.TaskAlreadyAssigned,
                    string.Equals(snapshot.AssignedUserId, callerUserId, StringComparison.OrdinalIgnoreCase)
                        ? "Task is already directly assigned to you."
                        : "Task is already directly assigned to another user.");

            case WorkflowTaskAssignmentMode.Unassigned:
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.TaskNotClaimable,
                    "Unassigned tasks cannot be self-claimed. An administrator must reassign the task first.");

            case WorkflowTaskAssignmentMode.RoleQueue:
                EnsureCallerHoldsRole(snapshot.AssignedRole);
                break;

            case WorkflowTaskAssignmentMode.OrgQueue:
                EnsureCallerInOrg(snapshot.AssignedOrgId);
                break;

            default:
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                    $"Task has an unknown AssignmentMode '{snapshot.AssignmentMode}'.");
        }

        // 3. Build target tuple — DirectUser to caller.
        var target = new AssignmentTarget(
            Mode:   WorkflowTaskAssignmentMode.DirectUser,
            UserId: callerUserId,
            Role:   null,
            OrgId:  null);

        var trimmedReason = NormalizeReason(reason) ?? DefaultClaimReason;

        return await ApplyTransitionAsync(
            snapshot,
            target,
            trimmedReason,
            auditAction:      "workflow.task.claim",
            auditDescription: $"Task claimed from {snapshot.AssignmentMode}",
            ct);
    }

    // ===================== REASSIGN =====================

    public async Task<WorkflowTaskAssignmentResult> ReassignAsync(
        Guid taskId,
        ReassignTaskRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Reassign request body is required.");

        EnsureCallerIsAdmin();

        var reason = NormalizeReason(request.Reason)
            ?? throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.MissingAssignmentReason,
                "Reassignment requires a non-empty reason.");

        var targetMode = (request.TargetMode ?? string.Empty).Trim();
        if (!WorkflowTaskAssignmentMode.IsKnown(targetMode))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                $"Target mode '{request.TargetMode}' is not recognised. " +
                $"Allowed: DirectUser, RoleQueue, OrgQueue, Unassigned.");
        }

        var target = BuildAndValidateReassignTarget(targetMode, request);

        var snapshot = await ReadSnapshotAsync(taskId, ct);

        // State guard — Open or InProgress only. Terminal rejected.
        if (WorkflowTaskStatus.IsTerminal(snapshot.Status))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskStateInvalid,
                $"Task status '{snapshot.Status}' is terminal and cannot be reassigned.");
        }
        if (!string.Equals(snapshot.Status, WorkflowTaskStatus.Open, StringComparison.Ordinal) &&
            !string.Equals(snapshot.Status, WorkflowTaskStatus.InProgress, StringComparison.Ordinal))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskNotReassignable,
                $"Task status '{snapshot.Status}' is not reassignable.");
        }

        return await ApplyTransitionAsync(
            snapshot,
            target,
            reason,
            auditAction:      "workflow.task.reassign",
            auditDescription: $"Task reassigned {snapshot.AssignmentMode} → {target.Mode}",
            ct);
    }

    // =================== Internals: read + write ===================

    private async Task<TaskSnapshot> ReadSnapshotAsync(Guid taskId, CancellationToken ct)
    {
        // Task service is the read authority (post-TASK-FLOW-03).
        // Status is normalised to Flow PascalCase so all downstream
        // comparisons against WorkflowTaskStatus.* continue to work.
        var taskDto = await _taskClient.GetTaskByIdAsync(taskId, ct);

        if (taskDto is null)
            throw new NotFoundException(nameof(WorkflowTask), taskId);

        return new TaskSnapshot(
            Id:             taskDto.TaskId,
            WorkflowInstanceId: taskDto.WorkflowInstanceId ?? Guid.Empty,
            Status:         NormalizeStatus(taskDto.Status),
            AssignmentMode: taskDto.AssignmentMode ?? WorkflowTaskAssignmentMode.Unassigned,
            AssignedUserId: taskDto.AssignedUserId,
            AssignedRole:   taskDto.AssignedRole,
            AssignedOrgId:  taskDto.AssignedOrgId);
    }

    /// <summary>
    /// Delegates the assignment to the Task service and emits the audit
    /// event. The shadow <c>ExecuteUpdateAsync</c> CAS that existed
    /// before TASK-FLOW-03 has been removed — Task service is now the
    /// sole write authority.
    /// </summary>
    private async Task<WorkflowTaskAssignmentResult> ApplyTransitionAsync(
        TaskSnapshot      snapshot,
        AssignmentTarget  target,
        string?           reason,
        string            auditAction,
        string            auditDescription,
        CancellationToken ct)
    {
        EnsureSingleModeShape(target);

        var now   = DateTime.UtcNow;
        var actor = _user.UserId;

        var isUnassignedTarget = target.Mode == WorkflowTaskAssignmentMode.Unassigned;
        DateTime? assignedAtForRow = isUnassignedTarget ? null : now;
        string?   assignedByForRow = isUnassignedTarget ? null : actor;
        string?   reasonForRow     = isUnassignedTarget ? null : reason;

        var taskId    = snapshot.Id;
        var newMode   = target.Mode;
        var newUserId = target.UserId;
        var newRole   = target.Role;
        var newOrgId  = target.OrgId;

        // TASK-FLOW-02 — delegate assignment to Task service.
        Guid? assignedUserGuid = null;
        if (!string.IsNullOrWhiteSpace(newUserId))
        {
            if (Guid.TryParse(newUserId, out var parsed))
                assignedUserGuid = parsed;
            else
                _log.LogWarning(
                    "WorkflowTaskAssignmentService: AssignedUserId '{UserId}' is not a valid Guid — assignedUserId will be null in Task service.",
                    newUserId);
        }

        Guid tenantGuid = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(_user.TenantId) && !Guid.TryParse(_user.TenantId, out tenantGuid))
        {
            _log.LogWarning(
                "WorkflowTaskAssignmentService: TenantId '{TenantId}' is not a valid Guid — queue assignment to Task service skipped.",
                _user.TenantId);
        }

        if (tenantGuid != Guid.Empty)
        {
            try
            {
                await _taskClient.SetQueueAssignmentAsync(
                    tenantId:         tenantGuid,
                    taskId:           taskId,
                    assignmentMode:   newMode,
                    assignedUserId:   assignedUserGuid,
                    assignedRole:     newRole,
                    assignedOrgId:    newOrgId,
                    assignedBy:       actor,
                    assignmentReason: reasonForRow,
                    ct:               ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "WorkflowTaskAssignmentService: Task service SetQueueAssignment FAILED for task {TaskId} mode={Mode}. Propagating error.",
                    taskId, newMode);
                throw;
            }
        }

        _log.LogInformation(
            "WorkflowTask assignment transition: TaskId={TaskId} {PrevMode}→{NewMode} (Action={Action}, By={By})",
            taskId, snapshot.AssignmentMode, newMode, auditAction, actor);

        await EmitAuditAsync(
            taskId,
            snapshot.WorkflowInstanceId,
            snapshot.AssignmentMode,
            snapshot.AssignedUserId,
            snapshot.AssignedRole,
            snapshot.AssignedOrgId,
            target,
            reason,
            auditAction,
            auditDescription,
            occurredAtUtc: now,
            ct);

        return new WorkflowTaskAssignmentResult(
            TaskId:           taskId,
            WorkflowInstanceId: snapshot.WorkflowInstanceId,
            Status:           snapshot.Status,
            AssignmentMode:   target.Mode,
            AssignedUserId:   target.UserId,
            AssignedRole:     target.Role,
            AssignedOrgId:    target.OrgId,
            AssignedAt:       assignedAtForRow,
            AssignedBy:       assignedByForRow,
            AssignmentReason: reasonForRow,
            OccurredAtUtc:    now);
    }

    // ============== Internals: validation helpers ==============

    private static void EnsureSingleModeShape(AssignmentTarget target)
    {
        if (!WorkflowTaskAssignmentMode.IsKnown(target.Mode))
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                $"Target mode '{target.Mode}' is not a known mode.");

        switch (target.Mode)
        {
            case WorkflowTaskAssignmentMode.DirectUser:
                if (string.IsNullOrWhiteSpace(target.UserId) ||
                    target.Role is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "DirectUser target requires AssignedUserId and forbids AssignedRole / AssignedOrgId.");
                break;

            case WorkflowTaskAssignmentMode.RoleQueue:
                if (string.IsNullOrWhiteSpace(target.Role) ||
                    target.UserId is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "RoleQueue target requires AssignedRole and forbids AssignedUserId / AssignedOrgId.");
                break;

            case WorkflowTaskAssignmentMode.OrgQueue:
                if (string.IsNullOrWhiteSpace(target.OrgId) ||
                    target.UserId is not null || target.Role is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "OrgQueue target requires AssignedOrgId and forbids AssignedUserId / AssignedRole.");
                break;

            case WorkflowTaskAssignmentMode.Unassigned:
                if (target.UserId is not null || target.Role is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "Unassigned target forbids AssignedUserId / AssignedRole / AssignedOrgId.");
                break;
        }
    }

    private static AssignmentTarget BuildAndValidateReassignTarget(
        string             targetMode,
        ReassignTaskRequest req)
    {
        var userId = NullIfBlank(req.AssignedUserId);
        var role   = NullIfBlank(req.AssignedRole);
        var orgId  = NullIfBlank(req.AssignedOrgId);

        return new AssignmentTarget(targetMode, userId, role, orgId);
    }

    private void EnsureCallerHoldsRole(string? requiredRole)
    {
        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Task is in RoleQueue mode but its AssignedRole is missing; cannot evaluate claim eligibility.");
        }

        var heldByCaller = _user.Roles
            .Any(r => string.Equals(r, requiredRole, StringComparison.OrdinalIgnoreCase));

        if (!heldByCaller && !_user.IsPlatformAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                $"Caller does not hold the role '{requiredRole}' required to claim this task.");
        }
    }

    private void EnsureCallerInOrg(string? requiredOrgId)
    {
        if (string.IsNullOrWhiteSpace(requiredOrgId))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Task is in OrgQueue mode but its AssignedOrgId is missing; cannot evaluate claim eligibility.");
        }

        var callerOrg = _user.OrgId;
        var matches = !string.IsNullOrWhiteSpace(callerOrg)
            && string.Equals(callerOrg, requiredOrgId, StringComparison.OrdinalIgnoreCase);

        if (!matches && !_user.IsPlatformAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Caller is not a member of the organization that owns this task queue.");
        }
    }

    private void EnsureCallerIsAdmin()
    {
        var isAdmin = _user.IsPlatformAdmin
            || _user.Roles.Any(r =>
                string.Equals(r, RolePlatformAdmin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, RoleTenantAdmin, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Reassign requires platform-admin or tenant-admin authority.");
        }
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var trimmed = reason.Trim();
        if (trimmed.Length > MaxReasonLength)
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                $"Reason is too long ({trimmed.Length} chars). Maximum is {MaxReasonLength}.");
        }
        return trimmed;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Maps Task service UPPERCASE status strings to Flow's PascalCase
    /// <see cref="WorkflowTaskStatus"/> constants.
    /// </summary>
    private static string NormalizeStatus(string tsStatus) =>
        tsStatus switch
        {
            "OPEN"        => WorkflowTaskStatus.Open,
            "IN_PROGRESS" => WorkflowTaskStatus.InProgress,
            "COMPLETED"   => WorkflowTaskStatus.Completed,
            "CANCELLED"   => WorkflowTaskStatus.Cancelled,
            _             => tsStatus,
        };

    // ===================== Internals: audit =====================

    private async Task EmitAuditAsync(
        Guid             taskId,
        Guid             workflowInstanceId,
        string           prevMode,
        string?          prevUserId,
        string?          prevRole,
        string?          prevOrgId,
        AssignmentTarget target,
        string?          reason,
        string           action,
        string           description,
        DateTime         occurredAtUtc,
        CancellationToken ct)
    {
        try
        {
            var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["taskId"]               = taskId.ToString("D"),
                ["workflowInstanceId"]   = workflowInstanceId.ToString("D"),
                ["prevMode"]             = prevMode,
                ["prevAssignedUserId"]   = prevUserId,
                ["prevAssignedRole"]     = prevRole,
                ["prevAssignedOrgId"]    = prevOrgId,
                ["newMode"]              = target.Mode,
                ["newAssignedUserId"]    = target.UserId,
                ["newAssignedRole"]      = target.Role,
                ["newAssignedOrgId"]     = target.OrgId,
                ["reason"]               = reason,
                ["performedBy"]          = _user.UserId,
            };

            var evt = new AuditEvent(
                Action:        action,
                EntityType:    nameof(WorkflowTask),
                EntityId:      taskId.ToString("D"),
                TenantId:      _user.TenantId,
                UserId:        _user.UserId,
                Description:   description,
                Metadata:      metadata,
                OccurredAtUtc: occurredAtUtc);

            await _audit.WriteEventAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit emission failed for {Action} on TaskId={TaskId}; persisted state is unaffected.",
                action, taskId);
        }
    }

    // ===================== Internal records =====================

    private sealed record TaskSnapshot(
        Guid    Id,
        Guid    WorkflowInstanceId,
        string  Status,
        string  AssignmentMode,
        string? AssignedUserId,
        string? AssignedRole,
        string? AssignedOrgId);

    private sealed record AssignmentTarget(
        string  Mode,
        string? UserId,
        string? Role,
        string? OrgId);
}
