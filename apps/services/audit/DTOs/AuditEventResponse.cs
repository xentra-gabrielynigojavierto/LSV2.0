namespace PlatformAuditEventService.DTOs;

public sealed class AuditEventResponse
{
    public Guid   Id             { get; init; }
    public string Source         { get; init; } = string.Empty;
    public string EventType      { get; init; } = string.Empty;
    public string Category       { get; init; } = string.Empty;
    public string Severity       { get; init; } = string.Empty;
    public string? TenantId      { get; init; }
    public string? ActorId       { get; init; }
    public string? ActorLabel    { get; init; }
    public string? TargetType    { get; init; }
    public string? TargetId      { get; init; }
    public string Description    { get; init; } = string.Empty;
    public string Outcome        { get; init; } = string.Empty;
    public string? IpAddress     { get; init; }
    public string? CorrelationId { get; init; }
    public string? Metadata      { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset IngestedAtUtc { get; init; }
    public string? IntegrityHash { get; init; }
}
