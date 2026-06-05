using LegalSynq.AuditClient.DTOs;

namespace LegalSynq.AuditClient;

/// <summary>
/// Fire-and-observe client for the LegalSynq Platform Audit Event Service ingest API.
///
/// RULE: Never throw on delivery failure — return IngestResult.Accepted=false instead.
/// RULE: Persist-first, audit-second — do not gate business operations on audit success.
/// </summary>
public interface IAuditEventClient
{
    /// <summary>
    /// Submit a single audit event. Returns the assigned AuditId on success.
    /// Idempotent when IdempotencyKey is supplied.
    /// </summary>
    Task<IngestResult> IngestAsync(
        IngestAuditEventRequest request,
        CancellationToken       ct = default);

    /// <summary>
    /// Submit a batch of events (up to 500). Returns per-item results.
    /// Partial acceptance is possible — inspect BatchIngestResult.Results.
    /// </summary>
    Task<BatchIngestResult> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken  ct = default);
}
