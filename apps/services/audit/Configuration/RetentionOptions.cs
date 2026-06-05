namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Audit event retention policy options.
/// Bound from "Retention" section in appsettings.
/// Environment variable override prefix: Retention__
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// Default number of days to retain audit events.
    /// 0 = retain indefinitely (recommended for compliance audit trails).
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 0;

    /// <summary>
    /// Per-category retention overrides.
    /// Key = category name (e.g. "security"), Value = retention days (0 = indefinite).
    /// Example: { "system": 90, "debug": 30 }
    /// </summary>
    public Dictionary<string, int> CategoryOverrides { get; set; } = new();

    /// <summary>
    /// Per-tenant retention overrides.
    /// Key = tenantId, Value = retention days.
    /// Tenant-specific agreements may require longer or shorter windows.
    /// </summary>
    public Dictionary<string, int> TenantOverrides { get; set; } = new();

    /// <summary>
    /// When true, the RetentionPolicyJob is enabled and runs on schedule.
    /// </summary>
    public bool JobEnabled { get; set; } = false;

    /// <summary>
    /// Cron expression for the retention job schedule.
    /// Default: daily at 02:00 UTC.
    /// </summary>
    public string JobCronUtc { get; set; } = "0 2 * * *";

    /// <summary>
    /// Maximum number of records deleted per retention job run.
    /// Guards against large single-batch deletes that lock the table.
    /// </summary>
    public int MaxDeletesPerRun { get; set; } = 10_000;

    /// <summary>
    /// When true, expired records are archived before deletion.
    /// Requires <c>Archival:Strategy</c> to be set to a real provider (not None or NoOp).
    /// </summary>
    public bool ArchiveBeforeDelete { get; set; } = false;

    // ── Tier thresholds ───────────────────────────────────────────────────────

    /// <summary>
    /// Number of days from <c>RecordedAtUtc</c> during which a record is in the
    /// Hot storage tier and receives full primary-store access guarantees.
    ///
    /// After this many days, the record transitions to the Warm tier and becomes
    /// a candidate for archival to secondary storage.
    ///
    /// Set to 0 to disable the Hot/Warm distinction (all non-expired records are Hot).
    /// Default: 365 (1 year).
    /// </summary>
    public int HotRetentionDays { get; set; } = 365;

    // ── Safety controls ───────────────────────────────────────────────────────

    /// <summary>
    /// When true, the retention job evaluates and logs the policy result
    /// without archiving or deleting any records. This is the safe default
    /// and should remain true until the archival pipeline is fully validated
    /// in a production environment.
    ///
    /// Set to false only after confirming that:
    ///   1. <c>ArchiveBeforeDelete=true</c> and the archival provider is healthy.
    ///   2. Integrity checkpoints cover the archival window.
    ///   3. Legal hold checks are wired.
    /// </summary>
    public bool DryRun { get; set; } = true;

    // ── Legal hold (future) ───────────────────────────────────────────────────

    /// <summary>
    /// When true, the retention service checks the LegalHolds table before archiving or deleting
    /// any record. Records with an active hold (ReleasedAtUtc IS NULL) are classified as
    /// <see cref="Enums.StorageTier.LegalHold"/> and skipped regardless of their age.
    ///
    /// Requires the LegalHolds table to exist in the database (apply migrations first).
    /// Default: false (safe — no hold checks performed if the table is not yet migrated).
    /// </summary>
    public bool LegalHoldEnabled { get; set; } = false;

    // ── Scheduling ────────────────────────────────────────────────────────────

    /// <summary>
    /// Interval in hours between retention job runs when hosted as a BackgroundService.
    /// Default: 24 (daily). Must be > 0.
    /// Applies to <see cref="Jobs.RetentionHostedService"/>.
    /// </summary>
    public int RetentionIntervalHours { get; set; } = 24;

    // ── Batch delete ──────────────────────────────────────────────────────────

    /// <summary>
    /// Number of records to delete per database batch in Phase 2 enforcement.
    /// Smaller batches reduce table-lock duration and spread I/O over time.
    /// Default: 1 000.
    /// </summary>
    public int DeleteBatchSize { get; set; } = 1_000;
}
