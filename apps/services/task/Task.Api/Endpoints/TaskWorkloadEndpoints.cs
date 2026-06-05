using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// TASK-FLOW-03 — internal endpoints consumed by Flow's WorkloadService (E18)
/// and WorkflowTaskFromWorkflowFactory (dedup check). All routes are
/// protected by the <c>InternalService</c> policy (service-token only).
///
/// Routes:
///   GET /api/tasks/internal/flow-workload/{tenantId}/user-counts
///   GET /api/tasks/internal/flow-workload/{tenantId}/role/{role}/users
///   GET /api/tasks/internal/flow-workload/{tenantId}/org/{orgId}/users
///   GET /api/tasks/internal/flow-has-active-step
/// </summary>
public static class TaskWorkloadEndpoints
{
    public static void MapTaskWorkloadEndpoints(this WebApplication app) => Map(app);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks/internal")
            .RequireAuthorization("InternalService");

        group.MapGet("/flow-workload/{tenantId:guid}/user-counts", GetUserCounts);
        group.MapGet("/flow-workload/{tenantId:guid}/role/{role}/users", GetUsersByRole);
        group.MapGet("/flow-workload/{tenantId:guid}/org/{orgId}/users", GetUsersByOrg);
        group.MapGet("/flow-has-active-step", HasActiveStep);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetUserCounts(
        Guid                 tenantId,
        ITaskWorkloadService svc,
        string?              userIds = null,
        CancellationToken    ct      = default)
    {
        var ids = (userIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (ids.Count == 0)
            return Results.Ok(Array.Empty<object>());

        var counts = await svc.GetActiveCountsForUsersAsync(tenantId, ids, ct);

        return Results.Ok(counts.Select(c => new { UserId = c.UserId, Count = c.Count }));
    }

    private static async Task<IResult> GetUsersByRole(
        Guid                 tenantId,
        string               role,
        ITaskWorkloadService svc,
        int                  max = 20,
        CancellationToken    ct  = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Results.BadRequest("role must not be empty.");

        max = Math.Clamp(max, 1, 100);
        var users = await svc.GetUserIdsForRoleAsync(tenantId, role, max, ct);
        return Results.Ok(users);
    }

    private static async Task<IResult> GetUsersByOrg(
        Guid                 tenantId,
        string               orgId,
        ITaskWorkloadService svc,
        int                  max = 20,
        CancellationToken    ct  = default)
    {
        if (string.IsNullOrWhiteSpace(orgId))
            return Results.BadRequest("orgId must not be empty.");

        max = Math.Clamp(max, 1, 100);
        var users = await svc.GetUserIdsForOrgAsync(tenantId, orgId, max, ct);
        return Results.Ok(users);
    }

    private static async Task<IResult> HasActiveStep(
        ITaskWorkloadService svc,
        Guid              tenantId,
        Guid              workflowInstanceId,
        string            stepKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
            return Results.BadRequest("stepKey must not be empty.");

        var hasActive = await svc.HasActiveStepTaskAsync(tenantId, workflowInstanceId, stepKey, ct);
        return Results.Ok(new { HasActive = hasActive });
    }
}
