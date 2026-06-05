namespace PlatformAuditEventService.Entities;

/// <summary>
/// Lightweight registry of known source systems and services that emit audit events.
///
/// Design principles:
/// - Intentionally minimal. This is a reference/advisory record, not a hard enforcement gate.
///   Sources do not need to be pre-registered; IngestAuth handles authorization separately.
/// - Enables future extensibility: rate-limit config, per-source schema versions, event
///   type allowlists, or per-source retention overrides can be added here without a major
///   model change.
/// - IsActive allows a source to be administratively paused (e.g. during incident response)
///   without deleting the record or breaking foreign key relationships.
/// - The (SourceSystem, SourceService) pair is expected to be unique but is not enforced at
///   the model layer — uniqueness is enforced via a DB index in the DbContext configuration.
/// </summary>
public sealed class IngestSourceRegistration
{
    // ── Primary key ──────────────────────────────────────────────────────────

    /// <summary>Auto-increment surrogate key.</summary>
    public long Id { get; init; }

    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Logical system name. Must match the <see cref="AuditEventRecord.SourceSystem"/>
    /// value emitted by this source. Max 200 chars.
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Specific microservice or component within <see cref="SourceSystem"/>.
    /// Null means the registration covers all services within the system.
    /// </summary>
    public string? SourceService { get; init; }

    // ── State (mutable) ───────────────────────────────────────────────────────

    /// <summary>
    /// Whether this source is currently active.
    /// Inactive sources may be flagged during ingestion for administrative review.
    /// Does not block ingest by default; enforcement is controlled by IngestAuth config.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ── Documentation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Free-text notes for operators. Use to document the source's purpose, owner, or
    /// integration details. Not exposed via the public API.
    /// </summary>
    public string? Notes { get; set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When this registration was first created. Always UTC. Immutable.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
