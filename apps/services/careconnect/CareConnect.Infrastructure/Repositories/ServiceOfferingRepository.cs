using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ServiceOfferingRepository : IServiceOfferingRepository
{
    private readonly CareConnectDbContext _db;

    public ServiceOfferingRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ServiceOffering>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ServiceOfferings
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<ServiceOffering?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ServiceOfferings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);
    }

    public async Task<ServiceOffering?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        return await _db.ServiceOfferings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Code == code.ToUpper(), ct);
    }

    public async Task AddAsync(ServiceOffering offering, CancellationToken ct = default)
    {
        await _db.ServiceOfferings.AddAsync(offering, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ServiceOffering offering, CancellationToken ct = default)
    {
        _db.ServiceOfferings.Update(offering);
        await _db.SaveChangesAsync(ct);
    }
}
