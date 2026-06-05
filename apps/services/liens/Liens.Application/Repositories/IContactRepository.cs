using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<Contact> Items, int TotalCount)> SearchAsync(Guid tenantId, string? search, string? contactType, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Contact entity, CancellationToken ct = default);
    Task UpdateAsync(Contact entity, CancellationToken ct = default);
}
