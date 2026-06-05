// BLK-PERF-01: All read-only queries use AsNoTracking() to avoid EF Core change-tracking overhead.
using CareConnect.Application.DTOs;
using CareConnect.Application.Helpers;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ProviderRepository : IProviderRepository
{
    private readonly CareConnectDbContext _db;

    public ProviderRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<(List<Provider> Items, int TotalCount)> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildBaseQuery(tenantId, query);

        var totalCount = await baseQuery.CountAsync(ct);

        var ids = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => p.Id)
            .ToListAsync(ct);

        // BLK-PERF-01: AsNoTracking — provider list is read-only; no change tracking needed.
        var items = await _db.Providers
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<Provider>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildBaseQuery(tenantId, query)
            .Where(p => p.Latitude != null && p.Longitude != null);

        var ids = await baseQuery
            .OrderBy(p => p.Name)
            .Take(ProviderGeoHelper.MarkerLimit)
            .Select(p => p.Id)
            .ToListAsync(ct);

        // BLK-PERF-01: AsNoTracking — marker data is read-only.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Provider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — read-only detail fetch.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Provider provider, CancellationToken ct = default)
    {
        await _db.Providers.AddAsync(provider, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Provider provider, CancellationToken ct = default)
    {
        _db.Providers.Update(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default)
    {
        var existing = await _db.ProviderCategories
            .Where(pc => pc.ProviderId == providerId)
            .ToListAsync(ct);

        _db.ProviderCategories.RemoveRange(existing);

        if (categoryIds.Count > 0)
        {
            var newLinks = categoryIds.Select(cid => new ProviderCategory
            {
                ProviderId = providerId,
                CategoryId = cid
            });
            await _db.ProviderCategories.AddRangeAsync(newLinks, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<Provider?> GetByIdCrossAsync(Guid id, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — cross-tenant read used for public referral validation; read-only.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<Provider> BuildBaseQuery(Guid tenantId, GetProvidersQuery query)
    {
        // Providers are a platform-wide marketplace; all active providers from all tenants
        // are discoverable. The tenantId parameter is retained for future analytics/audit use.
        // BLK-PERF-01: AsNoTracking on base query — all search/marker flows are read-only.
        var q = _db.Providers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Name))
            q = q.Where(p => p.Name.Contains(query.Name));

        if (!string.IsNullOrWhiteSpace(query.CategoryCode))
            q = q.Where(p => p.ProviderCategories
                .Any(pc => pc.Category != null && pc.Category.Code == query.CategoryCode));

        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(p => p.City == query.City);

        if (!string.IsNullOrWhiteSpace(query.State))
            q = q.Where(p => p.State == query.State);

        if (query.AcceptingReferrals.HasValue)
            q = q.Where(p => p.AcceptingReferrals == query.AcceptingReferrals.Value);

        if (query.IsActive.HasValue)
            q = q.Where(p => p.IsActive == query.IsActive.Value);

        // LSCC-01-003: Admin filter — find provider linked to a specific Identity org
        if (query.OrganizationId.HasValue)
            q = q.Where(p => p.OrganizationId == query.OrganizationId.Value);

        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusMiles.HasValue)
        {
            var (minLat, maxLat, minLon, maxLon) = ProviderGeoHelper.BoundingBox(
                query.Latitude.Value, query.Longitude.Value, query.RadiusMiles.Value);

            q = q.Where(p =>
                p.Latitude  != null && p.Longitude != null &&
                p.Latitude  >= minLat && p.Latitude  <= maxLat &&
                p.Longitude >= minLon && p.Longitude <= maxLon);
        }

        if (query.NorthLat.HasValue && query.SouthLat.HasValue &&
            query.EastLng.HasValue  && query.WestLng.HasValue)
        {
            q = q.Where(p =>
                p.Latitude  != null && p.Longitude != null &&
                p.Latitude  >= query.SouthLat.Value && p.Latitude  <= query.NorthLat.Value &&
                p.Longitude >= query.WestLng.Value  && p.Longitude <= query.EastLng.Value);
        }

        return q;
    }

    public async Task<List<Provider>> GetUnlinkedAsync(Guid tenantId, CancellationToken ct = default)
    {
        // BLK-PERF-01: AsNoTracking — admin read-only list.
        return await _db.Providers
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.OrganizationId == null)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Provider?> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId, ct);
    }

    /// <inheritdoc />
    public async Task<Provider?> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default)
    {
        return await _db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId, ct);
    }
}
