using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAvailabilityTemplateRepository
{
    Task<List<ProviderAvailabilityTemplate>> GetByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default);
    Task<List<ProviderAvailabilityTemplate>> GetActiveByProviderAsync(Guid tenantId, Guid providerId, CancellationToken ct = default);
    Task<ProviderAvailabilityTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(ProviderAvailabilityTemplate template, CancellationToken ct = default);
    Task UpdateAsync(ProviderAvailabilityTemplate template, CancellationToken ct = default);
}
