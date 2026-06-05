using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IConversationTimelineService
{
    Task RecordAsync(
        Guid tenantId, Guid conversationId,
        string eventType, string actorType, string summary, string visibility,
        DateTime occurredAtUtc,
        string? eventSubType = null,
        Guid? actorId = null,
        string? actorDisplayName = null,
        string? metadataJson = null,
        Guid? relatedMessageId = null,
        Guid? relatedAssignmentId = null,
        Guid? relatedSlaId = null,
        CancellationToken ct = default);

    Task<TimelinePageResponse> GetTimelineAsync(
        Guid tenantId, Guid conversationId,
        TimelineQuery query,
        CancellationToken ct = default);
}
