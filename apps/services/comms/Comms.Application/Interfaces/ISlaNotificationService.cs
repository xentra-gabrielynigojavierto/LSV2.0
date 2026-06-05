using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface ISlaNotificationService
{
    Task<SlaTriggerEvaluationResponse> EvaluateAllAsync(Guid tenantId, Guid systemUserId, CancellationToken ct = default);
    Task<ConversationSlaTriggerStateResponse?> GetTriggerStateAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
