using Reports.Contracts.Observability;

namespace Reports.Api.Endpoints;

public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/metrics")
            .RequireAuthorization();

        group.MapGet("/", (IReportsMetrics metrics) =>
        {
            var snapshot = metrics.GetSnapshot();
            return Results.Ok(new
            {
                timestamp = DateTimeOffset.UtcNow,
                executions = new
                {
                    total = snapshot.TotalExecutions,
                    byProduct = snapshot.ExecutionsByProduct,
                },
                exports = new
                {
                    total = snapshot.TotalExports,
                    byFormat = snapshot.ExportsByFormat,
                },
                scheduleRuns = snapshot.TotalScheduleRuns,
                deliveries = new
                {
                    total = snapshot.TotalDeliveries,
                    byMethod = snapshot.DeliveriesByMethod,
                },
                failures = snapshot.TotalFailures,
            });
        });
    }
}
