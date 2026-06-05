using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Contracts;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Read-only alert history endpoint.
///
/// <para>Returns the most recent alerts for a specific monitored entity,
/// including both active (<c>IsActive=true</c>) and resolved
/// (<c>IsActive=false</c>) rows. This is the historical view that the
/// active-alerts endpoint does not expose.</para>
///
/// <para><b>Why EntityName and not EntityId?</b>
/// The Control Center's <c>SystemAlert</c> type carries <c>entityName</c>
/// (the display name, snapshotted at fire time) but not <c>MonitoredEntityId</c>.
/// Querying by name is consistent with the snapshot semantics of the
/// <c>monitoring_alerts</c> table — if the entity is renamed, history for
/// the old name and new name remain separate rows, which is correct.</para>
///
/// <para><b>Auth</b>: anonymous — same reasoning as all other monitoring read
/// endpoints (called by the Control Center backend inside the trust boundary).
/// </para>
/// </summary>
public static class MonitoringAlertHistoryEndpoints
{
    private const int DefaultLimit = 10;
    private const int MaxLimit     = 50;

    public static IEndpointRouteBuilder MapMonitoringAlertHistoryEndpoints(
        this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/monitoring/alerts").AllowAnonymous();
        read.MapGet("/history", GetHistoryAsync);
        return app;
    }

    private static async Task<IResult> GetHistoryAsync(
        string? entityName,
        int? limit,
        MonitoringDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return Results.BadRequest(new { error = "entityName query parameter is required." });
        }

        var appliedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        // Load all matching rows into memory first, then map.
        // The mapping calls MapSeverity() which cannot be translated to SQL.
        var rows = await db.MonitoringAlerts
            .AsNoTracking()
            .Where(a => a.EntityName == entityName)
            .OrderByDescending(a => a.TriggeredAtUtc)
            .Take(appliedLimit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var response = rows.Select(a => new MonitoringAlertResponse(
            AlertId:       a.Id,
            EntityId:      a.MonitoredEntityId,
            Name:          a.EntityName,
            Severity:      MapSeverity(a.AlertType),
            Message:       a.Message,
            CreatedAtUtc:  a.TriggeredAtUtc,     // TriggeredAtUtc is the "created" time for an alert
            ResolvedAtUtc: a.ResolvedAtUtc));

        return Results.Ok(response);
    }

    private static string MapSeverity(AlertType type) => type switch
    {
        AlertType.StatusDown => "Critical",
        _                    => "Warning",
    };
}
