using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Entities;
using AuditRecordQueryRequest = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;

namespace PlatformAuditEventService.Repositories;

/// <summary>
/// Persistence contract for canonical audit event records.
///
/// Append-only contract: no update or delete operations are exposed.
/// The only exception is that the ingest service may compute and update
/// the Hash/PreviousHash fields immediately after the initial append;
/// that is handled by appending a fully-populated entity (with hashes set
/// by the service before calling AppendAsync).
/// </summary>
public interface IAuditEventRecordRepository
{
    /// <summary>
    /// Persist a new, fully-populated audit event record.
    /// Throws on duplicate IdempotencyKey (unique index violation).
    /// </summary>
    Task<AuditEventRecord> AppendAsync(AuditEventRecord record, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a single record by its public <see cref="AuditEventRecord.AuditId"/>.
    ///
    /// When <paramref name="scopeFilter"/> is supplied the same predicate pipeline used
    /// by <see cref="QueryAsync"/> is applied before the AuditId point lookup, enforcing
    /// all scope constraints the caller's authorization produced (TenantId, OrganizationId,
    /// ActorId, MaxVisibility, etc.). Pass the post-authorization query object from the
    /// controller so the fetch is subject to the same rules as a filtered list query.
    ///
    /// Pass null (default) only for internal callers that operate outside the HTTP
    /// request scope (e.g. LegalHoldService) and whose access is governed separately.
    ///
    /// Returns null if not found or if the record does not satisfy the scope constraints.
    /// </summary>
    Task<AuditEventRecord?> GetByAuditIdAsync(
        Guid                     auditId,
        AuditRecordQueryRequest? scopeFilter = null,
        CancellationToken        ct          = default);

    /// <summary>
    /// Checks whether a record with the given idempotency key already exists.
    /// Used by the ingest pipeline before persisting to perform a lightweight dedup probe.
    /// Returns false when <paramref name="key"/> is null or empty.
    /// </summary>
    Task<bool> ExistsIdempotencyKeyAsync(string? key, CancellationToken ct = default);

    /// <summary>
    /// Execute a filtered, paginated query over persisted audit event records.
    /// TenantId is the primary isolation boundary; callers must enforce scope
    /// before passing a query object.
    /// </summary>
    Task<PagedResult<AuditEventRecord>> QueryAsync(
        AuditRecordQueryRequest query,
        CancellationToken ct = default);

    /// <summary>
    /// Return the total number of persisted audit event records.
    /// Used for health/diagnostic reporting.
    /// </summary>
    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Return the earliest and latest <c>OccurredAtUtc</c> across all records
    /// that match the given filter predicates.
    ///
    /// Used by the query service to populate time-range metadata on paginated
    /// responses. A single aggregate DB query is issued (GROUP BY 1).
    ///
    /// Returns (null, null) when no records match the filter.
    /// The pagination and sorting fields of <paramref name="filter"/> are ignored.
    /// </summary>
    Task<(DateTimeOffset? Earliest, DateTimeOffset? Latest)> GetOccurredAtRangeAsync(
        AuditRecordQueryRequest filter,
        CancellationToken ct = default);

    /// <summary>
    /// Return the most recent record in a (TenantId, SourceSystem) chain.
    /// Used by the ingest service to populate PreviousHash for chain integrity.
    /// Returns null if no prior record exists in the chain.
    /// </summary>
    Task<AuditEventRecord?> GetLatestInChainAsync(
        string? tenantId,
        string sourceSystem,
        CancellationToken ct = default);

    /// <summary>
    /// Stream the <c>Hash</c> values of all audit event records whose
    /// <c>RecordedAtUtc</c> falls within <c>[from, to)</c>, in ascending
    /// surrogate-Id (insertion) order.
    ///
    /// Intended for integrity checkpoint generation. Yields only the hash field
    /// to minimise data transfer — full records are not needed for aggregate hash computation.
    ///
    /// Null hashes (records ingested when signing was disabled) are yielded as-is.
    /// The caller is responsible for handling them (typically treated as empty string
    /// in the concatenation to preserve positional count accuracy).
    ///
    /// Caller must consume the enumerable within the scope of a single request /
    /// background job — the underlying DbContext is disposed when enumeration completes
    /// or the cancellation token fires.
    /// </summary>
    IAsyncEnumerable<string?> StreamHashesForWindowAsync(
        DateTimeOffset fromRecordedAtUtc,
        DateTimeOffset toRecordedAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Stream filtered audit event records as an async enumerable.
    ///
    /// Intended for the export worker, which must iterate potentially millions of
    /// records without loading the full result set into memory. Unlike
    /// <see cref="QueryAsync"/>, this method does not paginate — the caller is
    /// responsible for writing each record to the output stream as it arrives.
    ///
    /// Ordering: ascending by OccurredAtUtc, then by Id (insertion order) for
    /// deterministic, reproducible export files.
    ///
    /// The pagination fields (Page, PageSize, SortBy, SortDescending) on the
    /// <paramref name="filter"/> object are ignored; only filter predicates apply.
    ///
    /// Caller must consume the enumerable within the scope of a single request /
    /// background job — the underlying DbContext is disposed when the enumerable
    /// is fully consumed or the cancellation token fires.
    /// </summary>
    IAsyncEnumerable<AuditEventRecord> StreamForExportAsync(
        AuditRecordQueryRequest filter,
        CancellationToken ct = default);

    // ── Retention enforcement ──────────────────────────────────────────────────

    /// <summary>
    /// Return the oldest records whose RecordedAtUtc is before the given cutoff,
    /// ordered oldest-first (RecordedAtUtc ASC).
    ///
    /// Used by the retention pipeline to identify records eligible for archival
    /// or deletion. The caller is responsible for pre-checking legal holds on each
    /// returned record before passing it to the archival or deletion pipeline.
    ///
    /// Returns at most <paramref name="batchSize"/> records per call.
    /// </summary>
    Task<IReadOnlyList<AuditEventRecord>> GetOldestEligibleAsync(
        DateTimeOffset    beforeRecordedAtUtc,
        int               batchSize,
        CancellationToken ct = default);

    /// <summary>
    /// Delete records whose primary key is in the provided set.
    ///
    /// DANGER: This is a destructive operation. Only call after confirming:
    ///   1. The record has been successfully archived (ArchivalResult.IsSuccess = true).
    ///   2. The record has no active legal hold.
    ///   3. Retention:DryRun = false.
    ///
    /// Returns the count of actually deleted rows.
    /// Uses EF Core ExecuteDeleteAsync for efficiency (no entity tracking required).
    /// </summary>
    Task<int> DeleteBatchAsync(
        IReadOnlyList<long> ids,
        CancellationToken   ct = default);
}
