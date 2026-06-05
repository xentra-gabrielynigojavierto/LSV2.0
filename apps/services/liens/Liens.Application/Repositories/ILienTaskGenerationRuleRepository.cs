using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskGenerationRuleRepository
{
    Task<List<LienTaskGenerationRule>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<LienTaskGenerationRule>> GetActiveByTenantAndEventAsync(Guid tenantId, string eventType, CancellationToken ct = default);
    Task<LienTaskGenerationRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(LienTaskGenerationRule entity, CancellationToken ct = default);
    Task UpdateAsync(LienTaskGenerationRule entity, CancellationToken ct = default);
}
