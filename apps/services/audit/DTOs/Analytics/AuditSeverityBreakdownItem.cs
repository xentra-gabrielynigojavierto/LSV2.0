namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>Event count for a single SeverityLevel.</summary>
public sealed class AuditSeverityBreakdownItem
{
    /// <summary>Severity label (e.g. "Info", "Warn", "Error").</summary>
    public required string Severity { get; init; }

    /// <summary>Numeric severity value (for ordered display on the client).</summary>
    public required int SeverityValue { get; init; }

    /// <summary>Total events at this severity within the query scope.</summary>
    public required long Count { get; init; }
}
