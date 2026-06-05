using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly CareConnectDbContext _db;

    public FacilityRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<Facility>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Facilities
            .Where(f => f.TenantId == tenantId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
    }

    public async Task<Facility?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Facilities
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == id, ct);
    }

    public async Task AddAsync(Facility facility, CancellationToken ct = default)
    {
        await _db.Facilities.AddAsync(facility, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Facility facility, CancellationToken ct = default)
    {
        _db.Facilities.Update(facility);
        await _db.SaveChangesAsync(ct);
    }
}
