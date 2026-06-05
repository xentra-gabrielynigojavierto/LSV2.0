using PlatformAuditEventService.DTOs.Ingest;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Contract for the audit event ingestion pipeline.
///
/// Responsibilities:
///   - Idempotency enforcement (reject duplicate IdempotencyKey submissions)
///   - AuditId + RecordedAtUtc generation (server-side, not caller-controlled)
///   - Integrity hash computation and chain linking (PreviousHash)
///   - Append-only persistence via the underlying record repository
///   - Structured per-item results that let callers distinguish accepted vs. rejected
///
/// This interface is intentionally narrow. It is NOT responsible for:
///   - Validation (handled by FluentValidation before reaching the service)
///   - Authorization (handled by middleware)
///   - Response projection (handled by the controller mapping layer)
///
/// Transport extensibility:
///   The interface exposes only the logical ingest contract. The concrete implementation
///   (<see cref="AuditEventIngestionService"/>) uses <see cref="Repositories.IAuditEventRecordRepository"/>
///   as its persistence transport. Swapping to a queued or outbox-driven transport
///   requires only a different repository implementation registered in DI — the
///   interface and its callers (controllers) are unchanged.
///
///   Planned future transports (not yet implemented):
///     DirectDatabase    — current default; synchronous write to AuditEventRecords
///     QueuedIngest      — write to a message bus; worker picks up and persists
///     OutboxDriven      — write to a transactional outbox; background relay persists
/// </summary>
public interface IAuditEventIngestionService
{
    /// <summary>
    /// Ingest a single validated audit event and return a structured result.
    ///
    /// The <paramref name="request"/> must have already passed FluentValidation.
    /// The service enforces idempotency and returns <see cref="IngestItemResult"/>
    /// rather than throwing on duplicate or persistence failures, so the caller
    /// can translate failures to the appropriate HTTP status code.
    /// </summary>
    Task<IngestItemResult> IngestSingleAsync(
        IngestAuditEventRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Ingest a batch of validated audit events and return aggregate + per-item results.
    ///
    /// Semantics controlled by <see cref="BatchIngestRequest.StopOnFirstError"/>:
    ///   false (default) — all items attempted independently; per-item results report each outcome.
    ///   true            — processing halts after the first rejection; remaining items are Skipped.
    ///
    /// <see cref="BatchIngestRequest.BatchCorrelationId"/> is propagated as a fallback
    /// CorrelationId for any item that does not supply its own.
    /// </summary>
    Task<BatchIngestResponse> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken ct = default);
}
