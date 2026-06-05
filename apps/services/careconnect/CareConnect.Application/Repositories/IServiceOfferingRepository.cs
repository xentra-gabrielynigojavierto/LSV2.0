using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IServiceOfferingRepository
{
    Task<List<ServiceOffering>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<ServiceOffering?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ServiceOffering?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task AddAsync(ServiceOffering offering, CancellationToken ct = default);
    Task UpdateAsync(ServiceOffering offering, CancellationToken ct = default);
}
