using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Entities;

/// <summary>
/// Tracks a request to export a filtered slice of audit records to a file.
///
/// Design principles:
/// - Export jobs are created atomically and processed asynchronously by the export worker.
/// - Immutable identity fields (ExportId, RequestedBy, Scope, Filter, Format) are init-only.
/// - Mutable lifecycle fields (Status, FilePath, ErrorMessage, CompletedAtUtc) use regular
///   setters so the worker can update them without creating a new instance.
/// - FilterJson carries the serialized query predicate so the worker can reproduce the exact
///   result set without coupling to the originating HTTP request.
/// </summary>
public sealed class AuditExportJob
{
    // ── Primary key ──────────────────────────────────────────────────────────

    /// <summary>Auto-increment surrogate key.</summary>
    public long Id { get; init; }

    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Public identifier for this export job. Exposed in API responses.
    /// UUIDv7 recommended for time-ordered inserts.
    /// </summary>
    public required Guid ExportId { get; init; }

    /// <summary>
    /// ActorId of the principal who requested the export (user, service account, etc.).
    /// </summary>
    public required string RequestedBy { get; init; }

    // ── Scope ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Organizational level the export is scoped to.
    /// Controls which records the export worker is permitted to include.
    /// </summary>
    public required ScopeType ScopeType { get; init; }

    /// <summary>
    /// The concrete scope identifier (tenantId, organizationId, userId, etc.)
    /// corresponding to <see cref="ScopeType"/>. Null for Global scope.
    /// </summary>
    public string? ScopeId { get; init; }

    // ── Filter ────────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON-serialized filter predicate applied when selecting records for this export.
    /// Persisted so the worker can reconstruct the query independently.
    /// Stored as raw text; schema is defined by the export request DTO.
    /// </summary>
    public string? FilterJson { get; init; }

    // ── Output configuration ──────────────────────────────────────────────────

    /// <summary>
    /// Requested output format. Valid values: "Json", "Csv", "Ndjson".
    /// Validated by the ingest controller against ExportOptions.SupportedFormats.
    /// </summary>
    public required string Format { get; init; }

    // ── Lifecycle (mutable) ───────────────────────────────────────────────────

    /// <summary>
    /// Current processing state. Updated by the export worker.
    /// </summary>
    public ExportStatus Status { get; set; } = ExportStatus.Pending;

    /// <summary>
    /// Absolute or relative path to the output file, set when Status = Completed.
    /// Null while processing or when the job failed.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Human-readable error description, set when Status = Failed.
    /// Null for all other states.
    /// </summary>
    public string? ErrorMessage { get; set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When the export job was submitted. Set at creation, immutable.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// When the export reached a terminal state (Completed, Failed, Cancelled, Expired).
    /// Null while Pending or Processing.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>
    /// Number of records written into the export file.
    /// Set by the export worker when Status transitions to Completed.
    /// Null while Pending, Processing, or when the job Failed.
    /// </summary>
    public long? RecordCount { get; set; }
}
