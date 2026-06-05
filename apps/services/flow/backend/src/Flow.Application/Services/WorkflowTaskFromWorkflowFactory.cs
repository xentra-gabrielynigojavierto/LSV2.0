using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.2 — production implementation of
/// <see cref="IWorkflowTaskFromWorkflowFactory"/>. See the interface
/// docs and <c>analysis/E11.2-report.md</c> for the rule set.
///
/// <para>
/// <b>TASK-FLOW-03 (post-migration):</b> the shadow table
/// (<c>flow_workflow_tasks</c>) has been dropped. Dedup check #1
/// (EF Local change-tracker scan) has been removed — the change
/// tracker no longer holds WorkflowTask entities. Dedup check #2 now
/// delegates to
/// <see cref="IFlowTaskServiceClient.HasActiveStepTaskAsync"/> (Task
/// service as read authority). The shadow <c>_db.WorkflowTasks.Add</c>
/// write at the end of successful creation has been removed; the Task
/// service write (already performed via
/// <see cref="IFlowTaskServiceClient.CreateWorkflowTaskAsync"/>) is
/// the sole persistence point. The returned <see cref="WorkflowTask"/>
/// is constructed in-memory and is NOT EF-tracked.
/// </para>
/// </summary>
public sealed class WorkflowTaskFromWorkflowFactory : IWorkflowTaskFromWorkflowFactory
{
    private const int MaxTitleLength         = 512;
    private const int MaxAssignedUserIdLength = 256;
    private const int MaxAssignedRoleLength   = 128;
    private const int MaxAssignedOrgIdLength  = 256;

    private readonly IFlowDbContext _db;
    private readonly IWorkflowTaskAssignmentResolver _assignmentResolver;
    private readonly IWorkflowTaskSlaClock _slaClock;
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly ILogger<WorkflowTaskFromWorkflowFactory> _logger;

    public WorkflowTaskFromWorkflowFactory(
        IFlowDbContext db,
        IWorkflowTaskAssignmentResolver assignmentResolver,
        IWorkflowTaskSlaClock slaClock,
        IFlowTaskServiceClient taskClient,
        ILogger<WorkflowTaskFromWorkflowFactory> logger)
    {
        _db                 = db;
        _assignmentResolver = assignmentResolver;
        _slaClock           = slaClock;
        _taskClient         = taskClient;
        _logger             = logger;
    }

    public async Task<WorkflowTask?> EnsureForCurrentStepAsync(
        WorkflowInstance instance,
        CancellationToken cancellationToken = default)
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));

        // ── Eligibility ────────────────────────────────────────────────
        if (!string.Equals(instance.Status, WorkflowEngine.StatusActive, StringComparison.Ordinal))
        {
            return null;
        }

        var stepKey = instance.CurrentStepKey;
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return null;
        }

        var instanceId = instance.Id;
        var tenantId   = instance.TenantId;

        // ── Dedup: active Open / InProgress task at this step ─────────
        // Ask the Task service (write authority, post-TASK-FLOW-03)
        // whether an active task already exists for this instance+step.
        // Falls back to allowing creation when TenantId cannot be
        // parsed as a Guid (correctly-provisioned tenants always have
        // a Guid TenantId — a parse failure implies a misconfigured
        // environment).
        //
        // SECURITY / CORRECTNESS — we pass the TenantId from the
        // parent instance (same reasoning as the old IgnoreQueryFilters
        // + explicit TenantId predicate): platform-admin retry paths
        // load the parent instance cross-tenant and the ambient tenant
        // claim may not match instance.TenantId.
        if (Guid.TryParse(tenantId, out var tenantGuid))
        {
            var hasActive = await _taskClient.HasActiveStepTaskAsync(
                tenantGuid, instanceId, stepKey, cancellationToken);
            if (hasActive)
            {
                return null;
            }
        }

        // ── Title ─────────────────────────────────────────────────────
        var workflowName = await _db.FlowDefinitions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Id == instance.WorkflowDefinitionId
                     && d.TenantId == tenantId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var prefix = !string.IsNullOrWhiteSpace(workflowName)
            ? workflowName!
            : instance.ProductKey;

        var title = $"{prefix} — {stepKey}";
        if (title.Length > MaxTitleLength) title = title[..MaxTitleLength];

        // ── Stage the task (in-memory only, post-TASK-FLOW-03) ────────
        var task = new WorkflowTask
        {
            TenantId           = tenantId,
            WorkflowInstanceId = instanceId,
            StepKey            = stepKey,
            Title              = title,
            Status             = WorkflowTaskStatus.Open,
            Priority           = WorkflowTaskPriority.Normal,
        };

        // ── SLA / DueAt stamping ──────────────────────────────────────
        try
        {
            task.DueAt = _slaClock.ComputeDueAt(System.DateTime.UtcNow, task.Priority);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex,
                "WorkflowTaskSlaClock threw for instance={InstanceId} step={StepKey} priority={Priority}; persisting task with null DueAt.",
                instanceId, stepKey, task.Priority);
            task.DueAt = null;
        }

        // ── Assignment (E11.3) ───────────────────────────────────────
        WorkflowTaskAssignment assignment;
        try
        {
            assignment = _assignmentResolver.Resolve(instance, stepKey)
                         ?? WorkflowTaskAssignment.None;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AssignmentResolver threw for instance={InstanceId} step={StepKey}; falling back to unassigned.",
                instanceId, stepKey);
            assignment = WorkflowTaskAssignment.None;
        }

        ApplyAssignment(task, assignment);

        // ── TASK-FLOW-01/02: delegate creation to Task service ────────
        // Task service is the sole write authority (post-TASK-FLOW-03).
        var assignedUserIdForTaskService =
            task.AssignmentMode == WorkflowTaskAssignmentMode.DirectUser
                ? task.AssignedUserId
                : null;

        try
        {
            await _taskClient.CreateWorkflowTaskAsync(
                workflowInstanceId: instanceId,
                stepKey:            stepKey,
                title:              task.Title,
                priority:           task.Priority,
                dueAt:              task.DueAt,
                assignedUserId:     assignedUserIdForTaskService,
                externalId:         task.Id,
                assignmentMode:     task.AssignmentMode,
                assignedRole:       task.AssignedRole,
                assignedOrgId:      task.AssignedOrgId,
                assignedBy:         task.AssignedBy,
                assignmentReason:   task.AssignmentReason,
                ct:                 cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WorkflowTaskFromWorkflowFactory.Ensure: Task service create FAILED for instance={InstanceId} step={StepKey}. " +
                "Propagating error.",
                instanceId, stepKey);
            throw;
        }

        _logger.LogInformation(
            "WorkflowTaskFromWorkflowFactory.Ensure created task instance={InstanceId} tenant={TenantId} step={StepKey} title={Title} assignedType={AssignedType}",
            instanceId, tenantId, stepKey, title, AssignmentTypeOf(task));

        // Return the in-memory entity (NOT EF-tracked post-TASK-FLOW-03).
        // All existing callers discard this return value; it is retained
        // for interface compatibility and to carry the assigned Id back
        // to callers that do inspect it.
        return task;
    }

    private static void ApplyAssignment(WorkflowTask task, WorkflowTaskAssignment assignment)
    {
        var now = DateTime.UtcNow;

        if (assignment.AssignedUserId is { } userId)
        {
            task.AssignedUserId = Truncate(userId, MaxAssignedUserIdLength);
            task.AssignedRole   = null;
            task.AssignedOrgId  = null;
            task.AssignmentMode = WorkflowTaskAssignmentMode.DirectUser;
            task.AssignedAt     = now;
            task.AssignedBy     = null;
            return;
        }

        if (assignment.AssignedRole is { } role)
        {
            task.AssignedUserId = null;
            task.AssignedRole   = Truncate(role, MaxAssignedRoleLength);
            task.AssignedOrgId  = null;
            task.AssignmentMode = WorkflowTaskAssignmentMode.RoleQueue;
            task.AssignedAt     = now;
            task.AssignedBy     = null;
            return;
        }

        if (assignment.AssignedOrgId is { } orgId)
        {
            task.AssignedUserId = null;
            task.AssignedRole   = null;
            task.AssignedOrgId  = Truncate(orgId, MaxAssignedOrgIdLength);
            task.AssignmentMode = WorkflowTaskAssignmentMode.OrgQueue;
            task.AssignedAt     = now;
            task.AssignedBy     = null;
            return;
        }

        task.AssignedUserId   = null;
        task.AssignedRole     = null;
        task.AssignedOrgId    = null;
        task.AssignmentMode   = WorkflowTaskAssignmentMode.Unassigned;
        task.AssignedAt       = null;
        task.AssignedBy       = null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string AssignmentTypeOf(WorkflowTask task) =>
        task.AssignedUserId is not null ? "user"
      : task.AssignedRole   is not null ? "role"
      : task.AssignedOrgId  is not null ? "org"
      : "none";
}
