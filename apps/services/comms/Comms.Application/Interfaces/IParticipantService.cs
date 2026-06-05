using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IParticipantService
{
    Task<ParticipantResponse> AddAsync(Guid tenantId, Guid orgId, Guid userId, Guid conversationId, AddParticipantRequest request, CancellationToken ct = default);
    Task<List<ParticipantResponse>> ListByConversationAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
    Task DeactivateAsync(Guid tenantId, Guid conversationId, Guid participantId, Guid userId, CancellationToken ct = default);
}
