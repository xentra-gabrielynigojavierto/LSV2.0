using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/products", async (IdentityDbContext db) =>
        {
            var products = await db.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto(p.Id, p.Name, p.Code, p.Description, p.IsActive))
                .ToListAsync();

            return Results.Ok(products);
        });

        return routes;
    }
}

public record ProductDto(Guid Id, string Name, string Code, string? Description, bool IsActive);
