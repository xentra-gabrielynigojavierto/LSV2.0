using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class TenantEmailSenderConfigRepository : ITenantEmailSenderConfigRepository
{
    private readonly CommsDbContext _db;

    public TenantEmailSenderConfigRepository(CommsDbContext db)
    {
        _db = db;
    }

    public async Task<TenantEmailSenderConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task<List<TenantEmailSenderConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.DisplayName)
            .ToListAsync(ct);

    public async Task<TenantEmailSenderConfig?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsDefault && c.IsActive, ct);

    public async Task<TenantEmailSenderConfig?> GetByFromEmailAsync(Guid tenantId, string fromEmail, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.FromEmail == fromEmail.Trim().ToLowerInvariant(), ct);

    public async Task<List<TenantEmailSenderConfig>> GetDefaultsAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs
            .Where(c => c.TenantId == tenantId && c.IsDefault)
            .ToListAsync(ct);

    public async Task AddAsync(TenantEmailSenderConfig config, CancellationToken ct = default) =>
        await _db.TenantEmailSenderConfigs.AddAsync(config, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
