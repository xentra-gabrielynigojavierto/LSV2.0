using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IMessageAttachmentRepository
{
    Task<MessageAttachment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<MessageAttachment>> ListByMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default);
    Task<List<MessageAttachment>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(MessageAttachment entity, CancellationToken ct = default);
    Task UpdateAsync(MessageAttachment entity, CancellationToken ct = default);
}
