using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Authentication;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Maps admin alert action endpoints.
///
/// <para><b>Route group</b>: <c>/monitoring/admin/alerts</c>.
/// All routes require the <see cref="MonitoringPolicies.AdminWrite"/> policy
/// (satisfied only by a bearer JWT with the <c>PlatformAdmin</c> role;
/// service tokens are explicitly excluded from admin write access).</para>
///
/// <para><b>Manual-resolve / scheduler interaction</b>: manually resolving an
/// alert that belongs to an entity still in Down state will clear the active
/// alert row. The scheduler's dedup rule (Rule 2) will suppress new alerts
/// while <c>entity_current_status</c> still reports Down — no new alert fires
/// until the entity recovers and then goes Down again. This is an intentional
/// trade-off documented in MON-INT-03-002-report.md §9.</para>
/// </summary>
public static class MonitoringAlertEndpoints
{
    public static IEndpointRouteBuilder MapMonitoringAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/monitoring/admin/alerts")
                       .RequireAuthorization(MonitoringPolicies.AdminWrite);

        admin.MapPost("/{id:guid}/resolve", ResolveAsync);

        return app;
    }

    private static async Task<IResult> ResolveAsync(
        Guid id,
        MonitoringDbContext db,
        CancellationToken ct)
    {
        var alert = await db.MonitoringAlerts
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            .ConfigureAwait(false);

        if (alert is null)
        {
            return Results.NotFound(
                ProblemFactory.NotFound($"Alert '{id}' was not found."));
        }

        if (!alert.IsActive)
        {
            // Idempotent — already resolved.
            return Results.Ok(new
            {
                alertId       = alert.Id,
                status        = "already_resolved",
                resolvedAtUtc = alert.ResolvedAtUtc,
            });
        }

        alert.Resolve(DateTime.UtcNow);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            alertId       = alert.Id,
            status        = "resolved",
            resolvedAtUtc = alert.ResolvedAtUtc,
        });
    }
}
