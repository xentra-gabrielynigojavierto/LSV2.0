using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationAssignmentRepository
{
    Task<ConversationAssignment?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<List<ConversationAssignment>> ListByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default);
    Task<List<ConversationAssignment>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ConversationAssignment assignment, CancellationToken ct = default);
    Task UpdateAsync(ConversationAssignment assignment, CancellationToken ct = default);
}
