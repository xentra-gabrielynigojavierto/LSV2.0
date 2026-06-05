using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.Integrity;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Aggregate integrity checkpoint service.
///
/// Responsibilities:
/// <list type="number">
///   <item>Generate a new checkpoint by computing an aggregate hash over all audit event
///     record hashes in a specified <c>RecordedAtUtc</c> time window.</item>
///   <item>Retrieve checkpoint history with optional type and time-range filtering.</item>
/// </list>
///
/// Checkpoints are append-only. A failed or incorrect checkpoint cannot be deleted —
/// a new corrected one is generated instead. This preserves the audit trail for the
/// checkpoint process itself.
///
/// Hash algorithm: same as the per-record algorithm configured in <c>Integrity:Algorithm</c>.
/// The aggregate hash is computed over the ordered concatenation (by ascending surrogate Id)
/// of all individual record hashes within the window. Records with a null hash (e.g. generated
/// when signing was disabled) contribute an empty string to the concatenation, preserving
/// position so the record count remains accurate.
/// </summary>
public interface IIntegrityCheckpointService
{
    /// <summary>
    /// Generate a new integrity checkpoint over the given time window and persist it.
    ///
    /// The checkpoint covers all audit event records where
    /// <c>RecordedAtUtc ∈ [request.FromRecordedAtUtc, request.ToRecordedAtUtc)</c>.
    ///
    /// For large windows, this operation streams records from the database and computes
    /// the aggregate hash incrementally without loading the full window into memory.
    ///
    /// Returns the newly created <see cref="IntegrityCheckpointResponse"/>.
    /// </summary>
    Task<IntegrityCheckpointResponse> GenerateAsync(
        GenerateCheckpointRequest request,
        CancellationToken         ct = default);

    /// <summary>
    /// Retrieve a paginated, optionally filtered list of persisted checkpoints.
    ///
    /// Results are returned newest-first (descending <c>CreatedAtUtc</c>).
    /// Filtering by <see cref="CheckpointListQuery.Type"/> is applied as an exact match.
    /// Filtering by <c>From</c> / <c>To</c> applies to <c>CreatedAtUtc</c>.
    /// </summary>
    Task<PagedResult<IntegrityCheckpointResponse>> ListAsync(
        CheckpointListQuery query,
        CancellationToken   ct = default);
}
