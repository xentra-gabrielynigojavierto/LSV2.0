using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class SettingRepository : ISettingRepository
{
    private readonly TenantDbContext _db;
    private readonly IDbContextFactory<TenantDbContext> _dbFactory;

    public SettingRepository(TenantDbContext db, IDbContextFactory<TenantDbContext> dbFactory)
    {
        _db        = db;
        _dbFactory = dbFactory;
    }

    public Task<TenantSetting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Settings.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<TenantSetting>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Settings
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.SettingKey)
            .ThenBy(s => s.ProductKey)
            .ToListAsync(ct);
    }

    public Task<TenantSetting?> GetByKeyAsync(
        Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default) =>
        _db.Settings.FirstOrDefaultAsync(s =>
            s.TenantId   == tenantId &&
            s.SettingKey == settingKey &&
            s.ProductKey == productKey,
            ct);

    public async Task AddAsync(TenantSetting setting, CancellationToken ct = default)
    {
        await _db.Settings.AddAsync(setting, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantSetting setting, CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(TenantSetting setting, CancellationToken ct = default)
    {
        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync(ct);
    }
}
