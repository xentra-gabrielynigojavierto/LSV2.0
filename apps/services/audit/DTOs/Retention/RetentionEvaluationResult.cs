namespace PlatformAuditEventService.DTOs.Retention;

/// <summary>
/// Result of a retention policy evaluation scan.
///
/// All counts are non-negative. Tier counts reflect the sampled window
/// (up to <see cref="RetentionEvaluationRequest.SampleLimit"/> records); the
/// <see cref="TotalRecordsInStore"/> is always the live total from the database.
///
/// When <see cref="RetentionEvaluationRequest.SampleLimit"/> is smaller than
/// <see cref="TotalRecordsInStore"/>, only the oldest records are classified.
/// Actual tier distributions across the full dataset may differ.
/// </summary>
public sealed class RetentionEvaluationResult
{
    // ── Totals ────────────────────────────────────────────────────────────────

    /// <summary>Total records in the primary store (live count, not a sample).</summary>
    public required long TotalRecordsInStore { get; init; }

    /// <summary>Number of records classified in this evaluation run.</summary>
    public required long SampleRecordsClassified { get; init; }

    // ── Tier breakdown (sample) ───────────────────────────────────────────────

    /// <summary>Records within the hot retention window. Full query access.</summary>
    public required long RecordsInHotTier { get; init; }

    /// <summary>
    /// Records past the hot window but within full retention.
    /// Eligible for archival to secondary storage.
    /// </summary>
    public required long RecordsInWarmTier { get; init; }

    /// <summary>
    /// Records past the full retention window.
    /// Eligible for deletion after archival (requires explicit compliance workflow).
    /// </summary>
    public required long RecordsInColdTier { get; init; }

    /// <summary>Records with no configured retention limit (RetentionDays=0).</summary>
    public required long RecordsIndefinite { get; init; }

    /// <summary>
    /// Records on legal hold (exempt from all retention enforcement).
    /// Always 0 in v1 — legal hold tracking is a future extension.
    /// </summary>
    public required long RecordsOnLegalHold { get; init; }

    // ── Expiry detail ─────────────────────────────────────────────────────────

    /// <summary>
    /// Number of sampled records that have exceeded their retention window.
    /// Subset of <see cref="RecordsInColdTier"/>.
    /// </summary>
    public required long RecordsExpiredInSample { get; init; }

    /// <summary>
    /// Breakdown of expired records by event category (sampled window).
    /// Key = category name string; Value = count.
    /// </summary>
    public required IReadOnlyDictionary<string, long> ExpiredByCategory { get; init; }

    // ── Oldest record ─────────────────────────────────────────────────────────

    /// <summary>
    /// RecordedAtUtc of the oldest record in the sample.
    /// Useful for understanding the depth of the audit trail.
    /// Null when no records exist in the scanned window.
    /// </summary>
    public DateTimeOffset? OldestRecordedAtUtc { get; init; }

    // ── Policy summary ────────────────────────────────────────────────────────

    /// <summary>
    /// Human-readable description of the effective retention policy applied
    /// during this evaluation. Example:
    ///   "Default 2555 days (7 years). Overrides: Security=3650d, Debug=90d."
    /// </summary>
    public required string PolicySummary { get; init; }

    /// <summary>
    /// Whether this run was in dry-run mode (always true in v1).
    /// When true, no records were modified, archived, or deleted.
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>When this evaluation was performed.</summary>
    public required DateTimeOffset EvaluatedAtUtc { get; init; }
}
