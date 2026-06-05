using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

/// <summary>
/// LS-NOTIF-SMS-015: EF Core repository for SmsProviderQualitySnapshot.
/// Security: No phone numbers, credentials, or raw provider payloads are stored or returned.
/// </summary>
public class SmsProviderQualityRepository : ISmsProviderQualityRepository
{
    private readonly NotificationsDbContext _db;

    public SmsProviderQualityRepository(NotificationsDbContext db)
        => _db = db;

    public async Task SaveSnapshotsAsync(
        IReadOnlyList<SmsProviderQualitySnapshot> snapshots,
        CancellationToken ct)
    {
        if (snapshots.Count == 0) return;

        // Upsert: delete existing snapshots for same natural-key dimensions, then insert.
        // This keeps the table to one "latest" entry per provider/tenant/country/config dimension.
        foreach (var snap in snapshots)
        {
            var existing = await _db.SmsProviderQualitySnapshots
                .Where(s => s.ProviderType == snap.ProviderType
                         && s.TenantId == snap.TenantId
                         && s.ProviderConfigId == snap.ProviderConfigId
                         && s.CountryCode == snap.CountryCode
                         && s.ProviderOwnershipMode == snap.ProviderOwnershipMode)
                .OrderByDescending(s => s.CalculatedAt)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
                _db.SmsProviderQualitySnapshots.Remove(existing);
        }

        await _db.SmsProviderQualitySnapshots.AddRangeAsync(snapshots, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SmsProviderQualitySnapshot?> GetLatestAsync(
        string providerType,
        Guid?  tenantId,
        Guid?  providerConfigId,
        string? countryCode,
        CancellationToken ct)
    {
        return await _db.SmsProviderQualitySnapshots
            .Where(s => s.ProviderType == providerType
                     && s.TenantId == tenantId
                     && s.ProviderConfigId == providerConfigId
                     && s.CountryCode == countryCode)
            .OrderByDescending(s => s.CalculatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SmsProviderQualitySnapshot>> QueryAsync(
        SmsQualitySnapshotQuery query,
        CancellationToken ct)
    {
        var q = _db.SmsProviderQualitySnapshots.AsQueryable();

        if (!string.IsNullOrEmpty(query.ProviderType))
            q = q.Where(s => s.ProviderType == query.ProviderType);
        if (query.TenantId.HasValue)
            q = q.Where(s => s.TenantId == query.TenantId);
        if (query.ProviderConfigId.HasValue)
            q = q.Where(s => s.ProviderConfigId == query.ProviderConfigId);
        if (!string.IsNullOrEmpty(query.ProviderOwnershipMode))
            q = q.Where(s => s.ProviderOwnershipMode == query.ProviderOwnershipMode);
        if (!string.IsNullOrEmpty(query.CountryCode))
            q = q.Where(s => s.CountryCode == query.CountryCode);
        if (!string.IsNullOrEmpty(query.Region))
            q = q.Where(s => s.Region == query.Region);
        if (query.From.HasValue)
            q = q.Where(s => s.CalculatedAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(s => s.CalculatedAt <= query.To.Value);

        return await q
            .OrderByDescending(s => s.CalculatedAt)
            .Skip(query.Offset)
            .Take(Math.Min(query.Limit, 500))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SmsProviderQualitySnapshot>> GetLatestPerProviderAsync(
        Guid?   tenantId,
        string? countryCode,
        CancellationToken ct)
    {
        var q = _db.SmsProviderQualitySnapshots.AsQueryable();

        if (tenantId.HasValue)
            q = q.Where(s => s.TenantId == tenantId || s.TenantId == null);
        else
            q = q.Where(s => s.TenantId == null);

        if (!string.IsNullOrEmpty(countryCode))
            q = q.Where(s => s.CountryCode == countryCode);

        // Get latest per ProviderType using in-memory grouping (bounded result set)
        var all = await q
            .OrderByDescending(s => s.CalculatedAt)
            .Take(500)
            .ToListAsync(ct);

        return all
            .GroupBy(s => s.ProviderType)
            .Select(g => g.First())
            .ToList();
    }
}
