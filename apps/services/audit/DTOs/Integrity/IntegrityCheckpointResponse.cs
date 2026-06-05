namespace PlatformAuditEventService.DTOs.Integrity;

/// <summary>
/// API representation of an integrity checkpoint.
/// Returned by compliance and integrity verification endpoints.
/// </summary>
public sealed class IntegrityCheckpointResponse
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Internal checkpoint identifier.
    /// Exposed here as a stable reference for verification endpoints.
    /// </summary>
    public required long Id { get; init; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>
    /// Cadence or trigger label for this checkpoint.
    /// Examples: "hourly", "daily", "manual", "pre-audit-2026-Q2".
    /// </summary>
    public required string CheckpointType { get; init; }

    // ── Time window ───────────────────────────────────────────────────────────

    /// <summary>Start of the covered time window (inclusive), based on RecordedAtUtc.</summary>
    public required DateTimeOffset FromRecordedAtUtc { get; init; }

    /// <summary>End of the covered time window (exclusive), based on RecordedAtUtc.</summary>
    public required DateTimeOffset ToRecordedAtUtc { get; init; }

    // ── Integrity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// HMAC-SHA256 computed over the ordered concatenation of record hashes in the window.
    /// Compare against a fresh computation to detect tampering or deletion.
    /// </summary>
    public required string AggregateHash { get; init; }

    /// <summary>
    /// Number of records that were included in the AggregateHash computation.
    /// A mismatch with a live count indicates record deletion or insertion outside
    /// normal ingest.
    /// </summary>
    public required long RecordCount { get; init; }

    // ── Verification status ───────────────────────────────────────────────────

    /// <summary>
    /// Result of the most recent on-demand integrity verification against this checkpoint.
    /// Null when no verification has been performed since the checkpoint was created.
    /// True = AggregateHash matches a fresh recomputation.
    /// False = mismatch detected — tamper evidence present.
    /// </summary>
    public bool? IsValid { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent verification run.
    /// Null when IsValid is null.
    /// </summary>
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When this checkpoint record was created.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
