using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationSlaStateRepository
{
    Task<ConversationSlaState?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task AddAsync(ConversationSlaState slaState, CancellationToken ct = default);
    Task UpdateAsync(ConversationSlaState slaState, CancellationToken ct = default);
}
