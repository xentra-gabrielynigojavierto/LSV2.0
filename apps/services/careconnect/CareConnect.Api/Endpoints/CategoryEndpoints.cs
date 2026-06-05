using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using CareConnect.Application.Cache;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CareConnect.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/categories", async (
            ICategoryService  service,
            IMemoryCache      cache,
            CancellationToken ct) =>
        {
            // BLK-PERF-02: Categories are platform-wide reference data that change
            // only via admin action. Cache with a 5-minute TTL to eliminate repeated
            // DB round-trips across tenants. No tenant scope in the key because
            // categories contain NO tenant-specific data.
            var categories = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.Categories,
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.Categories;
                    entry.Size = 1;
                    return service.GetAllAsync(ct);
                });

            return Results.Ok(categories);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
