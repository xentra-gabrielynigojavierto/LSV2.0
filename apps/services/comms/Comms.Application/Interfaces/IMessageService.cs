using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IMessageService
{
    Task<MessageResponse> AddAsync(Guid tenantId, Guid orgId, Guid userId, Guid conversationId, AddMessageRequest request, CancellationToken ct = default);
    Task<List<MessageResponse>> ListByConversationAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
}
