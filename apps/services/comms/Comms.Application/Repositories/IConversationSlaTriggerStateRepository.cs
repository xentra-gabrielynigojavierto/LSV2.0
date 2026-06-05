using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationSlaTriggerStateRepository
{
    Task<ConversationSlaTriggerState?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default);
    Task UpdateAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default);
    Task<bool> TryUpdateAsync(ConversationSlaTriggerState triggerState, CancellationToken ct = default);
    Task<List<ConversationSlaTriggerState>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
