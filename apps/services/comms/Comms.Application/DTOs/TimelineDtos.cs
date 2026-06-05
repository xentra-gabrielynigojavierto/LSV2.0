namespace Comms.Application.DTOs;

public record TimelineEntryResponse(
    Guid Id,
    Guid ConversationId,
    string EventType,
    string? EventSubType,
    string ActorType,
    Guid? ActorId,
    string? ActorDisplayName,
    DateTime OccurredAtUtc,
    string Summary,
    string? MetadataJson,
    Guid? RelatedMessageId,
    Guid? RelatedAssignmentId,
    Guid? RelatedSlaId,
    string Visibility,
    DateTime CreatedAtUtc);

public record TimelinePageResponse(
    List<TimelineEntryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasMore);

public record TimelineQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    List<string>? EventTypes = null,
    bool IncludeInternal = true,
    int Page = 1,
    int PageSize = 50);
