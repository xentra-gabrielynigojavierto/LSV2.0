using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ICaseRepository
{
    Task<Case?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<Case?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default);
    Task<(List<Case> Items, int TotalCount)> SearchAsync(Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Case entity, CancellationToken ct = default);
    Task UpdateAsync(Case entity, CancellationToken ct = default);
}
