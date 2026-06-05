using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Documents.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness — is the process running? Only "live"-tagged checks (DB only, not ClamAV)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthJson,
        }).AllowAnonymous();

        // Readiness — are all dependencies ready? (DB + ClamAV)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate      = _ => true,
            ResponseWriter = WriteHealthJson,
        }).AllowAnonymous();
    }

    private static async Task WriteHealthJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode  = report.Status == HealthStatus.Unhealthy ? 503 : 200;

        var result = new
        {
            status        = report.Status.ToString().ToLowerInvariant(),
            service       = "documents",
            timestamp     = DateTime.UtcNow,
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks        = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                duration    = e.Value.Duration.TotalMilliseconds,
                tags        = e.Value.Tags,
            }),
        };

        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(result,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
