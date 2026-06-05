using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IMessageMentionRepository
{
    Task AddRangeAsync(IEnumerable<MessageMention> mentions, CancellationToken ct = default);
    Task<List<MessageMention>> ListByMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default);
    Task<List<MessageMention>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<List<MessageMention>> ListByMentionedUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
