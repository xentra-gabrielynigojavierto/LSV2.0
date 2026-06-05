using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IFacilityRepository
{
    Task<Facility?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<Facility> Items, int TotalCount)> SearchAsync(Guid tenantId, string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Facility entity, CancellationToken ct = default);
    Task UpdateAsync(Facility entity, CancellationToken ct = default);
}
