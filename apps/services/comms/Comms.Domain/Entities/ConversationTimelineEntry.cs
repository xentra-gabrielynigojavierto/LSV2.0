namespace Comms.Domain.Entities;

public sealed class ConversationTimelineEntry
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string? EventSubType { get; private set; }
    public string ActorType { get; private set; } = default!;
    public Guid? ActorId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Summary { get; private set; } = default!;
    public string? MetadataJson { get; private set; }
    public Guid? RelatedMessageId { get; private set; }
    public Guid? RelatedAssignmentId { get; private set; }
    public Guid? RelatedSlaId { get; private set; }
    public string Visibility { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private ConversationTimelineEntry() { }

    public static ConversationTimelineEntry Create(
        Guid tenantId,
        Guid conversationId,
        string eventType,
        string actorType,
        string summary,
        string visibility,
        DateTime occurredAtUtc,
        string? eventSubType = null,
        Guid? actorId = null,
        string? actorDisplayName = null,
        string? metadataJson = null,
        Guid? relatedMessageId = null,
        Guid? relatedAssignmentId = null,
        Guid? relatedSlaId = null)
    {
        return new ConversationTimelineEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            EventType = eventType,
            EventSubType = eventSubType,
            ActorType = actorType,
            ActorId = actorId,
            ActorDisplayName = actorDisplayName,
            OccurredAtUtc = occurredAtUtc,
            Summary = summary,
            MetadataJson = metadataJson,
            RelatedMessageId = relatedMessageId,
            RelatedAssignmentId = relatedAssignmentId,
            RelatedSlaId = relatedSlaId,
            Visibility = visibility,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
