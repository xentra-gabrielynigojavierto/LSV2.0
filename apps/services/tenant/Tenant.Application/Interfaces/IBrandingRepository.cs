using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface IBrandingRepository
{
    Task<TenantBranding?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task                  AddAsync(TenantBranding branding, CancellationToken ct = default);
    Task                  UpdateAsync(TenantBranding branding, CancellationToken ct = default);
}
