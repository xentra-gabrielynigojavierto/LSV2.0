using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IReadTrackingService
{
    Task<ReadStateResponse> MarkReadAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<ReadStateResponse> MarkUnreadAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<ReadStateResponse> GetReadStateAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
}
