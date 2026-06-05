namespace PlatformAuditEventService.DTOs;

/// <summary>
/// Query filter parameters for listing audit events.
/// </summary>
public sealed class AuditEventQueryRequest
{
    public string? Source      { get; set; }
    public string? EventType   { get; set; }
    public string? Category    { get; set; }
    public string? Severity    { get; set; }
    public string? TenantId    { get; set; }
    public string? ActorId     { get; set; }
    public string? TargetType  { get; set; }
    public string? TargetId    { get; set; }
    public string? Outcome     { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To   { get; set; }

    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
