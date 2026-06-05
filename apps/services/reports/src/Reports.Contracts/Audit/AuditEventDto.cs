namespace Reports.Contracts.Audit;

public sealed class AuditEventDto
{
    public string EventType { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string TenantId { get; init; } = string.Empty;
    public string? ProductCode { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string ActorUserId { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? RequestId { get; init; }
    public string Outcome { get; init; } = "Success";
    public string? MetadataJson { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
