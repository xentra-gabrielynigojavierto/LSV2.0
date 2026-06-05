using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// Flow integration endpoints.
///
/// POST /api/tasks/internal/flow-callback  — service-token protected; called by Flow or an orchestrator
///      when a workflow step transitions. Updates WorkflowStepKey on all linked tasks. Idempotent.
///
/// GET  /api/tasks/{id}/workflow-context   — returns the workflow linkage projection for a task.
/// PUT  /api/tasks/{id}/workflow-linkage   — admin-only; manually set workflow linkage on a task.
/// </summary>
public static class TaskFlowEndpoints
{
    public static void MapTaskFlowEndpoints(this WebApplication app)
    {
        // Internal callback — TASK-B05 (TASK-013): service-token only.
        // "InternalService" policy accepts only scheme=ServiceToken + role=service.
        // User JWTs (PlatformAdmin or otherwise) are explicitly rejected.
        var internalGroup = app.MapGroup("/api/tasks/internal")
            .RequireAuthorization("InternalService")
            .WithTags("Tasks - Flow Integration");

        internalGroup.MapPost("/flow-callback",    HandleFlowCallback);
        // TASK-FLOW-02 — new internal endpoints
        internalGroup.MapPost("/flow-sla-update",  HandleFlowSlaUpdate);
        internalGroup.MapPost("/flow-queue-assign/{tenantId:guid}/{taskId:guid}", HandleFlowQueueAssign);
        // TASK-FLOW-03 — cross-tenant SLA evaluation batch read
        internalGroup.MapGet("/flow-sla-batch",    HandleFlowSlaBatch);

        // Per-task workflow context — authenticated user
        var taskGroup = app.MapGroup("/api/tasks")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Tasks - Flow Integration");

        taskGroup.MapGet("/{id:guid}/workflow-context",  GetWorkflowContext);
        taskGroup.MapPut("/{id:guid}/workflow-linkage",  UpdateWorkflowLinkage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    /// <summary>
    /// Receives a workflow step-changed notification from the Flow service.
    /// Updates WorkflowStepKey on every PlatformTask that references the given WorkflowInstanceId.
    /// Returns a result summary including tasks updated and tasks skipped (already on that step).
    /// </summary>
    private static async System.Threading.Tasks.Task<IResult> HandleFlowCallback(
        FlowStepCallbackRequest request,
        ITaskService            taskService,
        CancellationToken       ct = default)
    {
        var result = await taskService.ProcessFlowCallbackAsync(request, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetWorkflowContext(
        Guid                   id,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetWorkflowContextAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpdateWorkflowLinkage(
        Guid                         id,
        UpdateWorkflowLinkageRequest request,
        ITaskService                 taskService,
        ICurrentRequestContext       ctx,
        CancellationToken            ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateWorkflowLinkageAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    // TASK-FLOW-02 — batch SLA state push from Flow's WorkflowTaskSlaEvaluator
    private static async System.Threading.Tasks.Task<IResult> HandleFlowSlaUpdate(
        FlowSlaUpdateRequest    request,
        ITaskService            taskService,
        ICurrentRequestContext  ctx,
        CancellationToken       ct = default)
    {
        // SLA evaluator is per-tenant; tenant comes from the service-token header claim
        var tenantId = ctx.TenantId;
        if (tenantId is null)
            return Results.BadRequest("TenantId is required in service token claims.");

        var result = await taskService.UpdateFlowSlaStateAsync(tenantId.Value, request, ct);
        return Results.Ok(result);
    }

    // TASK-FLOW-02 — queue assignment update from Flow's WorkflowTaskAssignmentService
    private static async System.Threading.Tasks.Task<IResult> HandleFlowQueueAssign(
        Guid                   tenantId,
        Guid                   taskId,
        FlowQueueAssignRequest request,
        ITaskService           taskService,
        CancellationToken      ct = default)
    {
        var result = await taskService.SetFlowQueueAssignmentAsync(tenantId, taskId, request, ct);
        return result.Updated ? Results.Ok(result) : Results.NotFound(result);
    }

    // TASK-FLOW-03 — cross-tenant SLA evaluation batch read.
    // No tenant context required: the SLA evaluator is a background worker
    // that reads across all tenants. Query params:
    //   batchSize         — max rows to return (default 100)
    //   dueSoonMinutes    — DueSoon horizon in minutes from now (default 60)
    private static async System.Threading.Tasks.Task<IResult> HandleFlowSlaBatch(
        ITaskService      taskService,
        int               batchSize      = 100,
        int               dueSoonMinutes = 60,
        CancellationToken ct             = default)
    {
        var horizon = DateTime.UtcNow.AddMinutes(Math.Max(0, dueSoonMinutes));
        var result  = await taskService.GetFlowSlaBatchAsync(
            Math.Max(1, batchSize), horizon, ct);
        return Results.Ok(result);
    }
}
