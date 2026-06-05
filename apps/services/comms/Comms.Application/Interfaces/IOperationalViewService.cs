using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IOperationalViewService
{
    Task<OperationalQueryResponse> QueryConversationsAsync(
        Guid tenantId,
        Guid userId,
        OperationalQueryRequest request,
        CancellationToken ct = default);
}
