using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IOperationalService
{
    Task<ConversationSlaStateResponse?> GetSlaStateAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<ConversationSlaStateResponse> UpdatePriorityAsync(Guid tenantId, Guid conversationId, Guid userId, UpdateConversationPriorityRequest request, CancellationToken ct = default);
    Task<ConversationOperationalSummaryResponse?> GetOperationalSummaryAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task<List<ConversationOperationalSummaryResponse>> ListOperationalAsync(Guid tenantId, OperationalListQuery query, CancellationToken ct = default);
    Task InitializeSlaAsync(Guid tenantId, Guid conversationId, string priority, DateTime startAtUtc, Guid userId, CancellationToken ct = default);
    Task SatisfyFirstResponseAsync(Guid tenantId, Guid conversationId, DateTime respondedAtUtc, Guid userId, CancellationToken ct = default);
    Task SatisfyResolutionAsync(Guid tenantId, Guid conversationId, DateTime resolvedAtUtc, Guid userId, CancellationToken ct = default);
    Task UpdateWaitingStateAsync(Guid tenantId, Guid conversationId, string waitingState, Guid userId, CancellationToken ct = default);
}
