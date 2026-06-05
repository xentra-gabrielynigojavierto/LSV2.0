namespace Documents.Domain.Entities;

/// <summary>
/// An in-process work item representing a file awaiting antivirus scanning.
/// Created on upload; processed asynchronously by the background scan worker.
/// </summary>
public sealed class ScanJob
{
    public required Guid    DocumentId    { get; init; }
    public required Guid    TenantId      { get; init; }

    /// <summary>Null = scan the document itself; non-null = scan a specific version.</summary>
    public Guid?    VersionId     { get; init; }

    public required string  StorageKey    { get; init; }
    public required string  FileName      { get; init; }
    public required string  MimeType      { get; init; }
    public DateTime EnqueuedAt  { get; init; } = DateTime.UtcNow;
    public int      AttemptCount { get; set; }

    /// <summary>
    /// HTTP correlation ID from the originating upload request.
    /// Threaded through the queue into the background worker and emitted in the
    /// completion event to enable end-to-end traceability across service boundaries.
    /// </summary>
    public string?  CorrelationId { get; init; }
}
