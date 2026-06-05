using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILookupValueRepository
{
    Task<LookupValue?> GetByIdAsync(Guid? tenantId, Guid id, CancellationToken ct = default);
    Task<List<LookupValue>> GetByCategoryAsync(Guid? tenantId, string category, CancellationToken ct = default);
    Task<LookupValue?> GetByCodeAsync(Guid? tenantId, string category, string code, CancellationToken ct = default);
    Task AddAsync(LookupValue entity, CancellationToken ct = default);
    Task UpdateAsync(LookupValue entity, CancellationToken ct = default);
}
