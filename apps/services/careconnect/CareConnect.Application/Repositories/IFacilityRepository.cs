using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IFacilityRepository
{
    Task<List<Facility>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Facility?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Facility facility, CancellationToken ct = default);
    Task UpdateAsync(Facility facility, CancellationToken ct = default);
}
