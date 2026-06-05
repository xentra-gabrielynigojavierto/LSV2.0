using Documents.Domain.Enums;

namespace Documents.Domain.Events;

/// <summary>
/// Emitted once, after a scan job reaches a final terminal state:
/// <see cref="ScanStatus.Clean"/>, <see cref="ScanStatus.Infected"/>, or <see cref="ScanStatus.Failed"/>.
///
/// The event is fire-and-forget from the scan pipeline's perspective.
/// Delivery failure must never corrupt document state.
/// </summary>
public sealed class DocumentScanCompletedEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    public Guid       EventId       { get; init; } = Guid.NewGuid();

    /// <summary>Name of the originating service — for routing in multi-service consumers.</summary>
    public string     ServiceName   { get; init; } = "documents";

    /// <summary>Document that was scanned.</summary>
    public Guid       DocumentId    { get; init; }

    /// <summary>Tenant that owns the document.</summary>
    public Guid       TenantId      { get; init; }

    /// <summary>Specific version that was scanned, if applicable.</summary>
    public Guid?      VersionId     { get; init; }

    /// <summary>Terminal scan outcome: Clean, Infected, or Failed.</summary>
    public ScanStatus ScanStatus    { get; init; }

    /// <summary>UTC timestamp when the scan result was finalised.</summary>
    public DateTime   OccurredAt    { get; init; } = DateTime.UtcNow;

    /// <summary>HTTP correlation ID from the originating upload request, if available.</summary>
    public string?    CorrelationId { get; init; }

    /// <summary>Total number of scan attempts (1 = succeeded on first try).</summary>
    public int        AttemptCount  { get; init; }

    /// <summary>ClamAV engine version string, if the scan reached the engine.</summary>
    public string?    EngineVersion { get; init; }

    /// <summary>Original file name — identifier only, never contains file contents.</summary>
    public string?    FileName      { get; init; }
}
