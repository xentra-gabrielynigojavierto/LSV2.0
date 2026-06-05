using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class MigrationEndpoints
{
    public static WebApplication MapMigrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/migration")
            .RequireAuthorization(Policies.AdminOnly);

        // ── Dry-run reconciliation (Block 4, unchanged) ───────────────────────

        group.MapGet("/dry-run", async (
            IMigrationUtilityService service,
            CancellationToken        ct) =>
        {
            var report = await service.RunDryRunAsync(ct);
            return Results.Ok(report);
        });

        // ── Execute migration (Block 5) ───────────────────────────────────────

        group.MapPost("/execute", async (
            [FromBody] MigrationExecuteRequest? body,
            IMigrationUtilityService            service,
            CancellationToken                   ct) =>
        {
            var request = body ?? new MigrationExecuteRequest();
            var result  = await service.ExecuteAsync(request, ct);
            return Results.Ok(result);
        });

        // ── Migration history list (Block 5) ──────────────────────────────────

        group.MapGet("/history", async (
            IMigrationUtilityService service,
            CancellationToken        ct,
            [FromQuery] int          limit = 20) =>
        {
            var history = await service.GetHistoryAsync(limit, ct);
            return Results.Ok(history);
        });

        // ── Migration run detail (Block 5) ────────────────────────────────────

        group.MapGet("/history/{runId:guid}", async (
            Guid                     runId,
            IMigrationUtilityService service,
            CancellationToken        ct) =>
        {
            var result = await service.GetRunAsync(runId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }
}
