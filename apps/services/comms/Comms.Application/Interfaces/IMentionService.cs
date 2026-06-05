using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IMentionService
{
    Task ProcessMentionsAsync(
        Guid tenantId, Guid conversationId, Guid messageId,
        Guid senderUserId, string messageBody,
        CancellationToken ct = default);

    Task<List<MentionResponse>> GetMentionsByMessageAsync(
        Guid tenantId, Guid messageId, CancellationToken ct = default);
}
