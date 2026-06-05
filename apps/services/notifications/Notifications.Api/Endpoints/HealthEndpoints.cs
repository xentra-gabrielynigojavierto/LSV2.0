using Microsoft.EntityFrameworkCore;
using Notifications.Infrastructure.Data;

namespace Notifications.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (NotificationsDbContext db) =>
        {
            try
            {
                await db.Database.CanConnectAsync();
                return Results.Ok(new { status = "healthy", service = "notifications", timestamp = DateTime.UtcNow });
            }
            catch
            {
                return Results.Json(new { status = "unhealthy", service = "notifications", timestamp = DateTime.UtcNow }, statusCode: 503);
            }
        }).WithTags("Health");

        app.MapGet("/info", () => Results.Ok(new
        {
            service = "notifications",
            version = "1.0.0",
            runtime = "dotnet-8.0",
            timestamp = DateTime.UtcNow
        })).WithTags("Health");
    }
}
