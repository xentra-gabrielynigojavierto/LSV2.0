using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IServicingItemRepository
{
    Task<ServicingItem?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ServicingItem?> GetByTaskNumberAsync(Guid tenantId, string taskNumber, CancellationToken ct = default);
    Task<(List<ServicingItem> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status, string? priority, string? assignedTo,
        Guid? caseId, Guid? lienId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(ServicingItem entity, CancellationToken ct = default);
    Task UpdateAsync(ServicingItem entity, CancellationToken ct = default);
}
