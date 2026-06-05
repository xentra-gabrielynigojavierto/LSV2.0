using BuildingBlocks.Authorization;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Infrastructure.Auth;

public sealed class PermissionService : IPermissionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IdentityDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(IdentityDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(
        IReadOnlyCollection<string> productRoleCodes,
        string permissionCode,
        CancellationToken ct = default)
    {
        var perms = await GetPermissionsAsync(productRoleCodes, ct);
        return perms.Contains(permissionCode);
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(
        IReadOnlyCollection<string> productRoleCodes,
        CancellationToken ct = default)
    {
        if (productRoleCodes.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cacheKey = BuildCacheKey(productRoleCodes);

        if (_cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        var perms = await _db.RolePermissionMappings
            .AsNoTracking()
            .Where(rc => productRoleCodes.Contains(rc.ProductRole.Code)
                      && rc.ProductRole.IsActive
                      && rc.Permission.IsActive)
            .Select(rc => rc.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

        IReadOnlySet<string> result =
            new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static string BuildCacheKey(IReadOnlyCollection<string> codes)
        => "perms:" + string.Join("|", codes.Order(StringComparer.OrdinalIgnoreCase));
}
