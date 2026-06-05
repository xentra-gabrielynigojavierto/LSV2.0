namespace PlatformAuditEventService.Entities;

/// <summary>
/// Represents an active or released legal hold placed on a specific audit event record.
///
/// A legal hold prevents the associated record from being archived or deleted by the
/// retention pipeline, regardless of the record's age or configured retention policy.
///
/// Legal holds must be explicitly created (e.g. by legal or compliance staff) and
/// explicitly released. They are never created automatically.
///
/// Design notes:
/// - A single audit record may have multiple holds over time (hold → release → re-hold).
/// - The active hold check uses <see cref="ReleasedAtUtc"/> is null as the "active" predicate.
/// - Holds are themselves audit-logged via the ingest pipeline when created or released.
/// - No navigation property to AuditEventRecord — resolved at the application layer.
/// </summary>
public sealed class LegalHold
{
    // ── Primary key ───────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-increment surrogate PK. Internal use only.
    /// </summary>
    public long Id { get; init; }

    // ── Business identifier ───────────────────────────────────────────────────

    /// <summary>
    /// Stable public identifier for this legal hold. Exposed in API responses.
    /// </summary>
    public required Guid HoldId { get; init; }

    // ── Target record ─────────────────────────────────────────────────────────

    /// <summary>
    /// The AuditId of the AuditEventRecord placed on hold.
    /// Must reference an existing record in AuditEventRecords.
    /// </summary>
    public required Guid AuditId { get; init; }

    // ── Hold metadata ─────────────────────────────────────────────────────────

    /// <summary>
    /// Identity of the user or service that created this hold (e.g. compliance officer's userId).
    /// </summary>
    public required string HeldByUserId { get; init; }

    /// <summary>
    /// When the hold was placed. Set by the API at creation time. Always UTC.
    /// </summary>
    public required DateTimeOffset HeldAtUtc { get; init; }

    /// <summary>
    /// When the hold was released. Null while the hold is active.
    /// Set by the release API. Always UTC.
    /// </summary>
    public DateTimeOffset? ReleasedAtUtc { get; set; }

    /// <summary>
    /// Identity of the user or service that released the hold. Null while active.
    /// </summary>
    public string? ReleasedByUserId { get; set; }

    /// <summary>
    /// Canonical legal authority reference. Examples: "litigation-hold-2026-001",
    /// "HIPAA-audit-2026", "subpoena-case-12345".
    /// Used by compliance workflows to group records under the same authority.
    /// </summary>
    public required string LegalAuthority { get; init; }

    /// <summary>
    /// Optional free-text notes about the hold (context, case reference, requester name).
    /// </summary>
    public string? Notes { get; init; }
}
