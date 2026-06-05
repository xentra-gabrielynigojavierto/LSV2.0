using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IAvailabilityExceptionRepository
{
    Task<ProviderAvailabilityException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<ProviderAvailabilityException>> GetByProviderAsync(Guid tenantId, Guid providerId, bool? isActive, CancellationToken ct = default);
    Task<List<ProviderAvailabilityException>> GetActiveInRangeAsync(Guid tenantId, Guid providerId, DateTime from, DateTime to, CancellationToken ct = default);
    Task AddAsync(ProviderAvailabilityException exception, CancellationToken ct = default);
    Task UpdateAsync(ProviderAvailabilityException exception, CancellationToken ct = default);
}
