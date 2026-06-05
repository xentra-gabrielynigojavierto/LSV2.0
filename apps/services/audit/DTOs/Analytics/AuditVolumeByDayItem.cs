namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>Event count for a single calendar day (UTC).</summary>
public sealed class AuditVolumeByDayItem
{
    /// <summary>Calendar date in ISO-8601 format (yyyy-MM-dd).</summary>
    public required string Date { get; init; }

    /// <summary>Total events that occurred on this date within the query scope.</summary>
    public required long Count { get; init; }
}
