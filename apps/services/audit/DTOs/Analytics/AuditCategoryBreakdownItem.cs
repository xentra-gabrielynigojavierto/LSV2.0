namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>Event count for a single EventCategory.</summary>
public sealed class AuditCategoryBreakdownItem
{
    /// <summary>EventCategory name (e.g. "Security", "Access", "Compliance").</summary>
    public required string Category { get; init; }

    /// <summary>Numeric EventCategory value (for stable sorting on the client).</summary>
    public required int CategoryValue { get; init; }

    /// <summary>Total events in this category within the query scope.</summary>
    public required long Count { get; init; }
}
