using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface IDomainService
{
    Task<List<DomainResponse>>  ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<DomainResponse>        CreateAsync(Guid tenantId, CreateDomainRequest request, CancellationToken ct = default);
    Task<DomainResponse>        UpdateAsync(Guid tenantId, Guid domainId, UpdateDomainRequest request, CancellationToken ct = default);
    Task                        DeactivateAsync(Guid tenantId, Guid domainId, CancellationToken ct = default);
}
