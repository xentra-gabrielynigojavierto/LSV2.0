using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ICapabilityService
{
    Task<List<CapabilityResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<CapabilityResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CapabilityResponse>       CreateAsync(Guid tenantId, CreateCapabilityRequest request, CancellationToken ct = default);
    Task<CapabilityResponse>       UpdateAsync(Guid tenantId, Guid id, UpdateCapabilityRequest request, CancellationToken ct = default);
    Task                           DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
