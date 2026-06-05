using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IConversationService
{
    Task<ConversationResponse> CreateAsync(Guid tenantId, Guid orgId, Guid userId, CreateConversationRequest request, CancellationToken ct = default);
    Task<ConversationResponse?> GetByIdAsync(Guid tenantId, Guid id, Guid? currentUserId = null, CancellationToken ct = default);
    Task<List<ConversationResponse>> ListByContextAsync(Guid tenantId, string contextType, string contextId, Guid? currentUserId = null, CancellationToken ct = default);
    Task<ConversationResponse> UpdateStatusAsync(Guid tenantId, Guid id, Guid userId, UpdateConversationStatusRequest request, CancellationToken ct = default);
    Task<ConversationThreadResponse> GetThreadAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);
}
