using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class AvailabilityExceptionRepository : IAvailabilityExceptionRepository
{
    private readonly CareConnectDbContext _db;

    public AvailabilityExceptionRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<ProviderAvailabilityException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderAvailabilityExceptions
            .Where(e => e.TenantId == tenantId && e.Id == id)
            .Include(e => e.Facility)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ProviderAvailabilityException>> GetByProviderAsync(Guid tenantId, Guid providerId, bool? isActive, CancellationToken ct = default)
    {
        var query = _db.ProviderAvailabilityExceptions
            .Where(e => e.TenantId == tenantId && e.ProviderId == providerId)
            .Include(e => e.Facility)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(e => e.IsActive == isActive.Value);

        return await query
            .OrderBy(e => e.StartAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<ProviderAvailabilityException>> GetActiveInRangeAsync(Guid tenantId, Guid providerId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.ProviderAvailabilityExceptions
            .Where(e => e.TenantId == tenantId
                     && e.ProviderId == providerId
                     && e.IsActive
                     && e.StartAtUtc < to
                     && e.EndAtUtc > from)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ProviderAvailabilityException exception, CancellationToken ct = default)
    {
        await _db.ProviderAvailabilityExceptions.AddAsync(exception, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProviderAvailabilityException exception, CancellationToken ct = default)
    {
        _db.ProviderAvailabilityExceptions.Update(exception);
        await _db.SaveChangesAsync(ct);
    }
}
