using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IAssignmentService
{
    Task<ConversationAssignmentResponse> AssignAsync(Guid tenantId, Guid conversationId, Guid userId, AssignConversationRequest request, CancellationToken ct = default);
    Task<ConversationAssignmentResponse> ReassignAsync(Guid tenantId, Guid conversationId, Guid userId, ReassignConversationRequest request, CancellationToken ct = default);
    Task<ConversationAssignmentResponse> UnassignAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<ConversationAssignmentResponse> AcceptAsync(Guid tenantId, Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<ConversationAssignmentResponse?> GetByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
