using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// TASK-FLOW-04 — Internal analytics endpoints consumed exclusively by Flow's
/// <c>FlowAnalyticsService</c> after the <c>flow_workflow_tasks</c> shadow table
/// is dropped.
///
/// <para>
/// All endpoints require <c>InternalService</c> policy (service-token auth).
/// Tenant-scoped endpoints derive the tenant from the <c>{tenantId}</c> route
/// segment, matching the existing <c>flow-queue-assign</c> pattern.
/// The platform-summary endpoint is cross-tenant (no route tenantId).
/// </para>
///
/// Routes:
///   GET /api/tasks/internal/flow-analytics/{tenantId}/sla?windowStart=...&amp;windowEnd=...
///   GET /api/tasks/internal/flow-analytics/{tenantId}/queue
///   GET /api/tasks/internal/flow-analytics/{tenantId}/assignment?windowStart=...&amp;windowEnd=...
///   GET /api/tasks/internal/flow-analytics/platform-summary
/// </summary>
public static class TaskAnalyticsEndpoints
{
    public static void MapTaskAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/internal/flow-analytics")
            .RequireAuthorization("InternalService")
            .WithTags("Tasks - Flow Analytics");

        group.MapGet("/{tenantId:guid}/sla",         GetSlaAnalytics);
        group.MapGet("/{tenantId:guid}/queue",        GetQueueAnalytics);
        group.MapGet("/{tenantId:guid}/assignment",   GetAssignmentAnalytics);
        group.MapGet("/platform-summary",             GetPlatformSummary);
    }

    private static async System.Threading.Tasks.Task<IResult> GetSlaAnalytics(
        Guid                  tenantId,
        ITaskAnalyticsService analytics,
        DateTime?             windowStart = null,
        DateTime?             windowEnd   = null,
        CancellationToken     ct          = default)
    {
        var (start, end) = ResolveWindow(windowStart, windowEnd);
        var result = await analytics.GetSlaAnalyticsAsync(tenantId, start, end, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetQueueAnalytics(
        Guid                  tenantId,
        ITaskAnalyticsService analytics,
        CancellationToken     ct = default)
    {
        var result = await analytics.GetQueueAnalyticsAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetAssignmentAnalytics(
        Guid                  tenantId,
        ITaskAnalyticsService analytics,
        DateTime?             windowStart = null,
        DateTime?             windowEnd   = null,
        CancellationToken     ct          = default)
    {
        var (start, end) = ResolveWindow(windowStart, windowEnd);
        var result = await analytics.GetAssignmentAnalyticsAsync(tenantId, start, end, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetPlatformSummary(
        ITaskAnalyticsService analytics,
        CancellationToken     ct = default)
    {
        var result = await analytics.GetPlatformAnalyticsAsync(ct);
        return Results.Ok(result);
    }

    private static (DateTime start, DateTime end) ResolveWindow(DateTime? start, DateTime? end)
    {
        var now = DateTime.UtcNow;
        return (start ?? now.AddDays(-7), end ?? now);
    }
}
