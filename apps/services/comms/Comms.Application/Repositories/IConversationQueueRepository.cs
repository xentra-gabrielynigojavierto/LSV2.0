using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationQueueRepository
{
    Task<ConversationQueue?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ConversationQueue?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task<ConversationQueue?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<ConversationQueue>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(ConversationQueue queue, CancellationToken ct = default);
    Task UpdateAsync(ConversationQueue queue, CancellationToken ct = default);
}
