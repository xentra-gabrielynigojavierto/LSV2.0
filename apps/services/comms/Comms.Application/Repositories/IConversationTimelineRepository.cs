using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IConversationTimelineRepository
{
    Task AddAsync(ConversationTimelineEntry entry, CancellationToken ct = default);
    Task<List<ConversationTimelineEntry>> QueryAsync(
        Guid tenantId, Guid conversationId,
        DateTime? fromDate, DateTime? toDate,
        List<string>? eventTypes, bool includeInternal,
        int skip, int take,
        CancellationToken ct = default);
    Task<int> CountAsync(
        Guid tenantId, Guid conversationId,
        DateTime? fromDate, DateTime? toDate,
        List<string>? eventTypes, bool includeInternal,
        CancellationToken ct = default);
}
