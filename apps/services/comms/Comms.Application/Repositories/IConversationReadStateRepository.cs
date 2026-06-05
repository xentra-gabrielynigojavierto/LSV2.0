using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationReadStateRepository
{
    Task<ConversationReadState?> GetAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ConversationReadState entity, CancellationToken ct = default);
    Task UpdateAsync(ConversationReadState entity, CancellationToken ct = default);
}
