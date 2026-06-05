namespace Documents.Domain.Entities;

/// <summary>
/// Wraps a ScanJob dequeued from IScanJobQueue with the queue-backend's
/// message identifier. Callers must call AcknowledgeAsync or NackAsync
/// to complete or retry the job.
/// </summary>
public sealed class ScanJobLease
{
    public required ScanJob Job { get; init; }

    /// <summary>
    /// Backend-specific message identifier.
    /// Redis Streams: XADD message ID (e.g. "1743280000000-0").
    /// In-memory: empty string (not needed).
    /// </summary>
    public string MessageId { get; init; } = string.Empty;
}
