namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Wraps multiple audit events for atomic submission in a single HTTP request.
///
/// Batch processing semantics:
/// - Default (StopOnFirstError=false): all events are validated and attempted independently;
///   the response reports per-item success/failure.
/// - StopOnFirstError=true: processing halts after the first validation or persistence
///   failure; items after the failure position are not attempted and are marked Skipped.
///
/// Idempotency applies per-item via IngestAuditEventRequest.IdempotencyKey.
/// There is no batch-level idempotency key; callers that retry a failed batch
/// should rely on per-item keys.
/// </summary>
public sealed class BatchIngestRequest
{
    /// <summary>
    /// The events to ingest. Required; must contain at least one item.
    /// Maximum batch size is controlled by the service configuration.
    /// </summary>
    public IReadOnlyList<IngestAuditEventRequest> Events { get; set; } = [];

    /// <summary>
    /// Optional correlation ID spanning the entire batch submission.
    /// Propagated into each event's CorrelationId if the individual event does not
    /// supply its own. Useful for tracing a single upstream operation that generates
    /// multiple audit events.
    /// </summary>
    public string? BatchCorrelationId { get; set; }

    /// <summary>
    /// When true, processing stops after the first item failure and remaining items
    /// are not attempted. When false (default), all items are attempted and results
    /// reported independently.
    /// </summary>
    public bool StopOnFirstError { get; set; } = false;
}
