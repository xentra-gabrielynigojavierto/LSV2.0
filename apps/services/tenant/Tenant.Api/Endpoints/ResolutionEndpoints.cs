using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class ResolutionEndpoints
{
    public static void MapResolutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/public/resolve");

        // ── GET /api/v1/public/resolve/by-host?host=acme.legalsynq.net ────────
        //
        // Query-string is used for the host parameter (rather than a path segment)
        // to avoid routing issues with dots in ASP.NET Core path segments.
        group.MapGet("/by-host", async (
            string              host,
            IResolutionService  svc,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(host))
                return Results.BadRequest(new { error = new { code = "invalid_input", message = "The 'host' query parameter is required." } });

            var result = await svc.ResolveByHostAsync(host, ct);

            return result is null
                ? Results.NotFound(new { error = new { code = "not_found", message = $"No active tenant resolved for host '{host}'." } })
                : Results.Ok(result);
        })
        .AllowAnonymous();

        // ── GET /api/v1/public/resolve/by-subdomain/{subdomain} ───────────────
        group.MapGet("/by-subdomain/{subdomain}", async (
            string              subdomain,
            IResolutionService  svc,
            CancellationToken   ct) =>
        {
            var result = await svc.ResolveBySubdomainAsync(subdomain, ct);

            return result is null
                ? Results.NotFound(new { error = new { code = "not_found", message = $"No active tenant resolved for subdomain '{subdomain}'." } })
                : Results.Ok(result);
        })
        .AllowAnonymous();

        // ── GET /api/v1/public/resolve/by-code/{code} ─────────────────────────
        group.MapGet("/by-code/{code}", async (
            string              code,
            IResolutionService  svc,
            CancellationToken   ct) =>
        {
            var result = await svc.ResolveByCodeAsync(code, ct);

            return result is null
                ? Results.NotFound(new { error = new { code = "not_found", message = $"No tenant found with code '{code}'." } })
                : Results.Ok(result);
        })
        .AllowAnonymous();

        // ── GET /api/v1/public/resolve/by-id/{id:guid} ────────────────────────
        // Used by cross-tenant public directory lookups (Common CareConnect portal).
        group.MapGet("/by-id/{id:guid}", async (
            Guid                id,
            IResolutionService  svc,
            CancellationToken   ct) =>
        {
            var result = await svc.ResolveByIdAsync(id, ct);

            return result is null
                ? Results.NotFound(new { error = new { code = "not_found", message = $"No tenant found with id '{id}'." } })
                : Results.Ok(result);
        })
        .AllowAnonymous();
    }
}
