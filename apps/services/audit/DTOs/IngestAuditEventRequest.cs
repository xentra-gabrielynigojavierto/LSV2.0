namespace PlatformAuditEventService.DTOs;

/// <summary>
/// Payload for ingesting a single audit/event record from a distributed system.
/// </summary>
public sealed class IngestAuditEventRequest
{
    public string  Source         { get; set; } = string.Empty;
    public string  EventType      { get; set; } = string.Empty;
    public string  Category       { get; set; } = string.Empty;
    public string  Severity       { get; set; } = "INFO";
    public string? TenantId       { get; set; }
    public string? ActorId        { get; set; }
    public string? ActorLabel     { get; set; }
    public string? TargetType     { get; set; }
    public string? TargetId       { get; set; }
    public string  Description    { get; set; } = string.Empty;
    public string  Outcome        { get; set; } = "SUCCESS";
    public string? IpAddress      { get; set; }
    public string? UserAgent      { get; set; }
    public string? CorrelationId  { get; set; }
    public string? Metadata       { get; set; }

    /// <summary>
    /// UTC time the event occurred in the source system. Defaults to server receipt time if omitted.
    /// </summary>
    public DateTimeOffset? OccurredAtUtc { get; set; }
}
