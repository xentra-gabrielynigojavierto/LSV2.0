using Comms.Application.DTOs;

namespace Comms.Application.Repositories;

public interface IOperationalConversationQueryRepository
{
    Task<(List<ConversationOperationalListItemResponse> Items, int TotalCount)> QueryAsync(
        Guid tenantId,
        OperationalQueryRequest request,
        Guid currentUserId,
        CancellationToken ct = default);
}
