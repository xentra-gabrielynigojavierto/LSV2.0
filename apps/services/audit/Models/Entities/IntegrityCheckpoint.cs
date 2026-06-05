namespace PlatformAuditEventService.Entities;

/// <summary>
/// Periodic snapshot of the aggregate integrity state over a time window of audit records.
///
/// Design principles:
/// - Checkpoints are written by a background job (hourly/daily) or on-demand by a
///   compliance operator. They are never updated — re-runs create new records.
/// - AggregateHash is an HMAC-SHA256 over the ordered concatenation of all individual
///   record Hashes in the [FromRecordedAtUtc, ToRecordedAtUtc) window.
/// - CheckpointType is an open string rather than an enum to allow custom cadences
///   without a schema migration (e.g. "weekly", "pre-audit", "manual-2026-Q1").
/// - RecordCount provides a fast consistency signal: if a record is deleted the count
///   will drift from the re-computed value before the hash mismatch is even checked.
/// </summary>
public sealed class IntegrityCheckpoint
{
    // ── Primary key ──────────────────────────────────────────────────────────

    /// <summary>Auto-increment surrogate key.</summary>
    public long Id { get; init; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>
    /// Descriptive label for the checkpoint cadence or trigger.
    /// Convention: "hourly" | "daily" | "weekly" | "manual" | custom label.
    /// Open string to avoid enum-driven schema migrations.
    /// </summary>
    public required string CheckpointType { get; init; }

    // ── Time window ───────────────────────────────────────────────────────────

    /// <summary>
    /// Start of the time window (inclusive) based on RecordedAtUtc of covered records.
    /// </summary>
    public required DateTimeOffset FromRecordedAtUtc { get; init; }

    /// <summary>
    /// End of the time window (exclusive) based on RecordedAtUtc of covered records.
    /// </summary>
    public required DateTimeOffset ToRecordedAtUtc { get; init; }

    // ── Integrity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// HMAC-SHA256 computed over the ordered concatenation of all individual record
    /// <see cref="AuditEventRecord.Hash"/> values within the time window, sorted by
    /// ascending <see cref="AuditEventRecord.Id"/>.
    /// Provides a single value to compare against a re-computation for tamper detection.
    /// </summary>
    public required string AggregateHash { get; init; }

    /// <summary>
    /// Number of records included in the AggregateHash computation.
    /// Must match the row count for the same window on re-verification.
    /// </summary>
    public required long RecordCount { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When this checkpoint record was written. Always UTC.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
