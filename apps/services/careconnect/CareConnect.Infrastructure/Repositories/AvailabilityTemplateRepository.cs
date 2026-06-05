using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AvailabilityTemplateRepository : IAvailabilityTemplateRepository
{
    private readonly CareConnectDbContext _db;

    public AvailabilityTemplateRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProviderAvailabilityTemplate>> GetByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default)
    {
        return await _db.ProviderAvailabilityTemplates
            .Where(t => t.TenantId == tenantId && t.ProviderId == providerId)
            .Include(t => t.Facility)
            .Include(t => t.ServiceOffering)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTimeLocal)
            .ToListAsync(ct);
    }

    public async Task<List<ProviderAvailabilityTemplate>> GetActiveByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default)
    {
        return await _db.ProviderAvailabilityTemplates
            .Where(t => t.TenantId == tenantId && t.ProviderId == providerId && t.IsActive)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTimeLocal)
            .ToListAsync(ct);
    }

    public async Task<ProviderAvailabilityTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderAvailabilityTemplates
            .Where(t => t.TenantId == tenantId && t.Id == id)
            .Include(t => t.Facility)
            .Include(t => t.ServiceOffering)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ProviderAvailabilityTemplate template, CancellationToken ct = default)
    {
        await _db.ProviderAvailabilityTemplates.AddAsync(template, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProviderAvailabilityTemplate template, CancellationToken ct = default)
    {
        _db.ProviderAvailabilityTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }
}
