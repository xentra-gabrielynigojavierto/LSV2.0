using Fund.Application;
using Fund.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fund.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly FundDbContext _db;

    public ApplicationRepository(FundDbContext db) => _db = db;

    public Task<Domain.Application?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Applications.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public Task<List<Domain.Application>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Applications
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(Domain.Application application, CancellationToken ct = default)
    {
        await _db.Applications.AddAsync(application, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Domain.Application application, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
