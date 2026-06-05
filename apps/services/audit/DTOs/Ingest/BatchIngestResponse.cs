namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Summary result of a batch ingest operation.
/// Contains aggregate counts and per-item detail.
/// </summary>
public sealed class BatchIngestResponse
{
    /// <summary>Total number of events submitted in the batch.</summary>
    public int Submitted { get; init; }

    /// <summary>Number of events that were accepted and persisted.</summary>
    public int Accepted { get; init; }

    /// <summary>
    /// Number of events that were rejected (validation failure, duplicate, error)
    /// or skipped (StopOnFirstError triggered).
    /// </summary>
    public int Rejected { get; init; }

    /// <summary>Whether any item in the batch was rejected or skipped.</summary>
    public bool HasErrors => Rejected > 0;

    /// <summary>Per-item results in the same order as the submitted Events list.</summary>
    public IReadOnlyList<IngestItemResult> Results { get; init; } = [];

    /// <summary>
    /// Echo of BatchCorrelationId from the request, if provided.
    /// </summary>
    public string? BatchCorrelationId { get; init; }
}
