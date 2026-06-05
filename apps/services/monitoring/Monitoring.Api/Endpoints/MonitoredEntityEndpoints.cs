using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Authentication;
using Monitoring.Api.Contracts;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Maps the monitored entity registry endpoints. CRUD-style only — no
/// monitoring execution, status evaluation, alerting, filtering, pagination,
/// or delete (all explicitly out of scope per MON-B02-002).
///
/// Auth: all endpoints below require authentication. <c>/health</c> remains
/// public and is mapped elsewhere. The Monitoring Service is an internal
/// platform service, so even read endpoints are protected.
/// </summary>
public static class MonitoredEntityEndpoints
{
    public static IEndpointRouteBuilder MapMonitoredEntityEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/monitoring/entities").RequireAuthorization();
        var admin = app.MapGroup("/monitoring/admin/entities")
                       .RequireAuthorization(MonitoringPolicies.AdminWrite);

        read.MapGet("/", ListAsync);
        read.MapGet("/{id:guid}", GetByIdAsync);

        admin.MapPost("/", CreateAsync);
        admin.MapPatch("/{id:guid}", UpdateAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(MonitoringDbContext db, CancellationToken ct)
    {
        // Stable, predictable ordering: by Name (asc), then CreatedAtUtc (asc)
        // as a tie-breaker. Documented in the report.
        var items = await db.MonitoredEntities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ThenBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        return Results.Ok(items.Select(MonitoredEntityResponse.From));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        MonitoringDbContext db,
        CancellationToken ct)
    {
        var entity = await db.MonitoredEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return entity is null
            ? Results.NotFound(ProblemFactory.NotFound($"Monitored entity '{id}' was not found."))
            : Results.Ok(MonitoredEntityResponse.From(entity));
    }

    private static async Task<IResult> CreateAsync(
        CreateMonitoredEntityRequest? request,
        MonitoringDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(ProblemFactory.BadRequest("Request body is required."));
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name)) missing.Add(nameof(request.Name));
        if (request.EntityType is null) missing.Add(nameof(request.EntityType));
        if (request.MonitoringType is null) missing.Add(nameof(request.MonitoringType));
        if (string.IsNullOrWhiteSpace(request.Target)) missing.Add(nameof(request.Target));

        if (missing.Count > 0)
        {
            return Results.BadRequest(ProblemFactory.BadRequest(
                "One or more required fields are missing or blank.",
                new Dictionary<string, string[]>
                {
                    ["missing"] = missing.ToArray(),
                }));
        }

        // Apply API-layer defaults for omitted optional classification fields.
        // The domain still validates the resulting values, so an explicitly
        // blank Scope from the wire is rejected with a 400.
        var entity = new MonitoredEntity(
            id: Guid.NewGuid(),
            name: request.Name!,
            entityType: request.EntityType!.Value,
            monitoringType: request.MonitoringType!.Value,
            target: request.Target!,
            scope: request.Scope ?? MonitoredEntityDefaults.Scope,
            impactLevel: request.ImpactLevel ?? MonitoredEntityDefaults.Impact,
            isEnabled: request.IsEnabled ?? true);

        db.MonitoredEntities.Add(entity);
        await db.SaveChangesAsync(ct);

        var response = MonitoredEntityResponse.From(entity);
        return Results.Created($"/monitoring/entities/{entity.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateMonitoredEntityRequest? request,
        MonitoringDbContext db,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(ProblemFactory.BadRequest("Request body is required."));
        }

        var entity = await db.MonitoredEntities
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null)
        {
            return Results.NotFound(ProblemFactory.NotFound($"Monitored entity '{id}' was not found."));
        }

        // Patch semantics: omitted fields untouched; present fields applied
        // through domain methods so invariants hold.
        if (request.Name is not null)
        {
            entity.Rename(request.Name);
        }

        if (request.Target is not null)
        {
            entity.Retarget(request.Target);
        }

        if (request.EntityType is not null || request.MonitoringType is not null)
        {
            entity.ChangeClassification(
                request.EntityType ?? entity.EntityType,
                request.MonitoringType ?? entity.MonitoringType);
        }

        if (request.IsEnabled is not null)
        {
            if (request.IsEnabled.Value) entity.Enable();
            else entity.Disable();
        }

        if (request.Scope is not null)
        {
            entity.Rescope(request.Scope);
        }

        if (request.ImpactLevel is not null)
        {
            entity.ChangeImpact(request.ImpactLevel.Value);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(MonitoredEntityResponse.From(entity));
    }
}
