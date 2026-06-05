using System.ComponentModel.DataAnnotations;

namespace PlatformAuditEventService.DTOs.Integrity;

/// <summary>
/// Request body for on-demand integrity checkpoint generation.
///
/// The service will stream all audit event records whose <c>RecordedAtUtc</c>
/// falls within <c>[FromRecordedAtUtc, ToRecordedAtUtc)</c>, concatenate their
/// individual hashes in ascending insertion-order (by surrogate Id), and persist
/// the resulting aggregate hash as a new <see cref="Entities.IntegrityCheckpoint"/>.
///
/// Caller scope requirement: <see cref="Authorization.CallerScope.PlatformAdmin"/>.
/// </summary>
public sealed class GenerateCheckpointRequest
{
    /// <summary>
    /// Descriptive label for this checkpoint's cadence or trigger.
    /// Convention: "hourly" | "daily" | "weekly" | "manual" | custom.
    /// Open string — no enum enforced so custom compliance runs are supported
    /// without schema changes (e.g. "pre-audit-2026-Q1", "manual-migration-verify").
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string CheckpointType { get; set; } = "manual";

    /// <summary>
    /// Inclusive start of the time window.
    /// Only records with <c>RecordedAtUtc >= FromRecordedAtUtc</c> are included.
    /// </summary>
    [Required]
    public DateTimeOffset FromRecordedAtUtc { get; set; }

    /// <summary>
    /// Exclusive end of the time window.
    /// Only records with <c>RecordedAtUtc &lt; ToRecordedAtUtc</c> are included.
    /// </summary>
    [Required]
    public DateTimeOffset ToRecordedAtUtc { get; set; }
}
