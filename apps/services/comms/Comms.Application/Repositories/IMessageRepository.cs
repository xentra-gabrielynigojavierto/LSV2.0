using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid tenantId, Guid conversationId, Guid messageId, CancellationToken ct = default);
    Task<List<Message>> ListByConversationOrderedAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<Message?> GetLatestByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(Message entity, CancellationToken ct = default);
}
