using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<Conversation>> ListByContextAsync(Guid tenantId, string contextType, string contextId, CancellationToken ct = default);
    Task<List<Conversation>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Conversation entity, CancellationToken ct = default);
    Task UpdateAsync(Conversation entity, CancellationToken ct = default);
}
