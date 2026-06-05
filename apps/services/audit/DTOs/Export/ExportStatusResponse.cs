using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Export;

/// <summary>
/// Current state of an export job.
/// Returned immediately after job creation and on subsequent status polls.
/// </summary>
public sealed class ExportStatusResponse
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Platform-assigned export job identifier. Use for status polling.</summary>
    public required Guid ExportId { get; init; }

    // ── Scope (echo) ──────────────────────────────────────────────────────────

    /// <summary>Echo of the scope type from the original request.</summary>
    public required ScopeType ScopeType { get; init; }

    /// <summary>Echo of the scope ID from the original request.</summary>
    public string? ScopeId { get; init; }

    // ── Output ────────────────────────────────────────────────────────────────

    /// <summary>Echo of the requested output format.</summary>
    public required string Format { get; init; }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Current lifecycle state of the export job.</summary>
    public required ExportStatus Status { get; init; }

    /// <summary>
    /// Human-readable status label matching the Status enum value.
    /// Provided for API consumers that prefer string status values.
    /// </summary>
    public string StatusLabel => Status.ToString();

    // ── Result details ────────────────────────────────────────────────────────

    /// <summary>
    /// Download URL or file path when Status = Completed.
    /// Format depends on the ExportOptions.Provider configuration:
    ///   Local   → relative file path
    ///   S3      → pre-signed URL (time-limited)
    ///   Azure   → SAS URL (time-limited)
    /// Null while Pending or Processing.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Number of records included in the export file.
    /// Null while Pending or Processing.
    /// </summary>
    public long? RecordCount { get; init; }

    /// <summary>
    /// Human-readable error description when Status = Failed.
    /// Null for all other states.
    /// </summary>
    public string? ErrorMessage { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When the export job was submitted.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// When the job reached a terminal state (Completed, Failed, Cancelled, Expired).
    /// Null while Pending or Processing.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    // ── Convenience ───────────────────────────────────────────────────────────

    /// <summary>True when the export is in a terminal state.</summary>
    public bool IsTerminal => Status is ExportStatus.Completed
        or ExportStatus.Failed
        or ExportStatus.Cancelled
        or ExportStatus.Expired;

    /// <summary>True when the output file is available for download.</summary>
    public bool IsAvailable => Status == ExportStatus.Completed && DownloadUrl is not null;
}
