using LegalSynq.AuditClient.Enums;

namespace LegalSynq.AuditClient.DTOs;

/// <summary>
/// Wire DTO for POST /internal/audit/events.
/// Field names and enum serialisation must match the Platform Audit Event Service ingest contract.
/// </summary>
public sealed class IngestAuditEventRequest
{
    public string          EventType      { get; set; } = string.Empty;
    public EventCategory   EventCategory  { get; set; } = EventCategory.Business;
    public string          SourceSystem   { get; set; } = string.Empty;
    public string?         SourceService  { get; set; }
    public VisibilityScope Visibility     { get; set; } = VisibilityScope.Tenant;
    public SeverityLevel   Severity       { get; set; } = SeverityLevel.Info;
    public DateTimeOffset  OccurredAtUtc  { get; set; } = DateTimeOffset.UtcNow;

    public AuditEventScopeDto  Scope  { get; set; } = new();
    public AuditEventActorDto? Actor  { get; set; }
    public AuditEventEntityDto? Entity { get; set; }

    public string  Action      { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string? Outcome     { get; set; }
    public string? Before      { get; set; }
    public string? After       { get; set; }
    public string? Metadata    { get; set; }

    public string?        CorrelationId  { get; set; }
    public string?        RequestId      { get; set; }
    public string?        SessionId      { get; set; }
    public string?        IdempotencyKey { get; set; }
    public List<string>?  Tags           { get; set; }
}
