using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/tenants", async (IdentityDbContext db) =>
        {
            var tenants = await db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new TenantDto(t.Id, t.Name, t.Code, t.IsActive))
                .ToListAsync();

            return Results.Ok(tenants);
        });

        return routes;
    }
}

public record TenantDto(Guid Id, string Name, string Code, bool IsActive);
