namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>A single event type ranked by event count.</summary>
public sealed class AuditTopEventTypeItem
{
    /// <summary>Dot-notation event type code (e.g. "identity.user.login.succeeded").</summary>
    public required string EventType { get; init; }

    /// <summary>Total events of this type within the query scope.</summary>
    public required long Count { get; init; }
}
