using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class SmsRoutingPolicyRepository : ISmsRoutingPolicyRepository
{
    private readonly NotificationsDbContext _db;

    public SmsRoutingPolicyRepository(NotificationsDbContext db) => _db = db;

    public async Task<SmsRoutingPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SmsRoutingPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<SmsRoutingPolicy> Items, int Total)> ListAsync(
        SmsRoutingPolicyQuery query, CancellationToken ct = default)
    {
        var q = _db.SmsRoutingPolicies.AsNoTracking().AsQueryable();

        if (query.TenantId.HasValue)
            q = q.Where(p => p.TenantId == query.TenantId || p.TenantId == null);
        if (query.Enabled.HasValue)
            q = q.Where(p => p.Enabled == query.Enabled.Value);
        if (!string.IsNullOrEmpty(query.RoutingMode))
            q = q.Where(p => p.RoutingMode == query.RoutingMode);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.CreatedAt)
            .Skip(query.Offset)
            .Take(Math.Max(1, Math.Min(query.Limit, 200)))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<SmsRoutingPolicy>> GetActiveForTenantAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _db.SmsRoutingPolicies
            .AsNoTracking()
            .Where(p => p.Enabled && (p.TenantId == tenantId || p.TenantId == null))
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

    public async Task<SmsRoutingPolicy> CreateAsync(SmsRoutingPolicy policy, CancellationToken ct = default)
    {
        _db.SmsRoutingPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
        return policy;
    }

    public async Task UpdateAsync(SmsRoutingPolicy policy, CancellationToken ct = default)
    {
        _db.SmsRoutingPolicies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    // ── Static mapper ─────────────────────────────────────────────────────────

    public static SmsRoutingPolicyDto ToDto(SmsRoutingPolicy p) => new()
    {
        Id                         = p.Id,
        TenantId                   = p.TenantId,
        Name                       = p.Name,
        Enabled                    = p.Enabled,
        Region                     = p.Region,
        CountryCode                = p.CountryCode,
        RoutingMode                = p.RoutingMode,
        PreferredProvidersJson     = p.PreferredProvidersJson,
        ExcludedProvidersJson      = p.ExcludedProvidersJson,
        MaxEstimatedCostPerMessage = p.MaxEstimatedCostPerMessage,
        RequireHealthyProvider     = p.RequireHealthyProvider,
        FallbackToPlatform         = p.FallbackToPlatform,
        Priority                   = p.Priority,
        CreatedAt                  = p.CreatedAt,
        UpdatedAt                  = p.UpdatedAt,
        CreatedBy                  = p.CreatedBy,
        UpdatedBy                  = p.UpdatedBy,
    };
}
