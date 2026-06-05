using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Export;

/// <summary>
/// Request to create an asynchronous audit record export job.
///
/// Submitted as a POST body. The service creates an <c>AuditExportJob</c> record,
/// enqueues the work, and returns an <see cref="ExportStatusResponse"/> with the
/// assigned ExportId. The caller polls GET /api/exports/{exportId} for completion.
///
/// Scope semantics:
/// - ScopeType + ScopeId define what the export worker is permitted to include.
/// - QueryAuth middleware validates that the caller holds a role sufficient to
///   export the requested scope.
/// - If EnforceTenantScope=true, a Tenant-scoped request's ScopeId is overridden
///   with the caller's tenantId claim.
/// </summary>
public sealed class ExportRequest
{
    // ── Scope ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Organizational level the export is bounded to.
    /// </summary>
    public ScopeType ScopeType { get; set; } = ScopeType.Tenant;

    /// <summary>
    /// Concrete scope ID (tenantId, organizationId, userId, etc.) matching ScopeType.
    /// Null for Global or Platform scope.
    /// </summary>
    public string? ScopeId { get; set; }

    // ── Filter ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Event category to export. Null exports all categories.
    /// </summary>
    public EventCategory? Category { get; set; }

    /// <summary>
    /// Only export events at or above this severity level.
    /// </summary>
    public SeverityLevel? MinSeverity { get; set; }

    /// <summary>
    /// Export only specific event type codes (OR-ed).
    /// </summary>
    public IReadOnlyList<string>? EventTypes { get; set; }

    /// <summary>
    /// Export events by a specific actor.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Export events targeting a specific resource type.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Export events targeting a specific resource ID. Best combined with EntityType.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Export events that occurred at or after this UTC timestamp (inclusive).
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Export events that occurred before this UTC timestamp (exclusive).
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Correlation ID to export (cross-service trace export).
    /// </summary>
    public string? CorrelationId { get; set; }

    // ── Output format ─────────────────────────────────────────────────────────

    /// <summary>
    /// Output file format. Validated against ExportOptions.SupportedFormats.
    /// Accepted values: "Json" | "Csv" | "Ndjson".
    /// </summary>
    public string Format { get; set; } = "Json";

    // ── Include options ───────────────────────────────────────────────────────

    /// <summary>
    /// Whether to include Before/After JSON snapshots in the export.
    /// May significantly increase file size for DataChange category events.
    /// Defaults to true.
    /// </summary>
    public bool IncludeStateSnapshots { get; set; } = true;

    /// <summary>
    /// Whether to include the Tags list for each record.
    /// </summary>
    public bool IncludeTags { get; set; } = true;

    /// <summary>
    /// Whether to include integrity Hash values for each record.
    /// Only honoured when the caller holds a role with ExposeIntegrityHash=true.
    /// </summary>
    public bool IncludeHashes { get; set; } = false;
}
