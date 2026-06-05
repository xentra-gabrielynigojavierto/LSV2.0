using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IQueueService
{
    Task<ConversationQueueResponse> CreateAsync(Guid tenantId, Guid userId, CreateConversationQueueRequest request, CancellationToken ct = default);
    Task<ConversationQueueResponse> UpdateAsync(Guid tenantId, Guid queueId, Guid userId, UpdateConversationQueueRequest request, CancellationToken ct = default);
    Task<ConversationQueueResponse?> GetByIdAsync(Guid tenantId, Guid queueId, CancellationToken ct = default);
    Task<List<ConversationQueueResponse>> ListAsync(Guid tenantId, CancellationToken ct = default);
}
