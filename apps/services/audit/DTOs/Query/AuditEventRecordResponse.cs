using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Query;

/// <summary>
/// Full API representation of a single persisted audit event record.
///
/// Design notes:
/// - Internal surrogate Id is not exposed; AuditId is the stable public identifier.
/// - Nested objects (Scope, Actor, Entity) mirror the ingest request shape for symmetry.
/// - Before, After, Metadata are returned as raw JSON strings; callers parse as needed.
/// - Tags is deserialized from TagsJson into a typed list.
/// - Hash is only populated when QueryAuth.ExposeIntegrityHash=true.
/// - IpAddress and UserAgent on Actor are redacted based on the caller's role.
/// </summary>
public sealed class AuditEventRecordResponse
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Platform-assigned stable public identifier for this record.</summary>
    public required Guid AuditId { get; init; }

    /// <summary>Source-assigned domain event ID, if provided at ingest.</summary>
    public Guid? EventId { get; init; }

    // ── Classification ────────────────────────────────────────────────────────

    public required string EventType { get; init; }
    public required EventCategory EventCategory { get; init; }

    // ── Source provenance ─────────────────────────────────────────────────────

    public required string SourceSystem { get; init; }
    public string? SourceService { get; init; }
    public string? SourceEnvironment { get; init; }

    // ── Scope / tenancy ───────────────────────────────────────────────────────

    /// <summary>Tenancy and organizational scope context.</summary>
    public required AuditEventScopeResponseDto Scope { get; init; }

    // ── Actor ─────────────────────────────────────────────────────────────────

    /// <summary>Actor identity context.</summary>
    public required AuditEventActorResponseDto Actor { get; init; }

    // ── Target entity ─────────────────────────────────────────────────────────

    /// <summary>Target resource, if the event was directed at a specific resource.</summary>
    public AuditEventEntityResponseDto? Entity { get; init; }

    // ── Action payload ────────────────────────────────────────────────────────

    public required string Action { get; init; }
    public required string Description { get; init; }

    /// <summary>Raw JSON string of the resource state before the action.</summary>
    public string? Before { get; init; }

    /// <summary>Raw JSON string of the resource state after the action.</summary>
    public string? After { get; init; }

    /// <summary>Raw JSON string of additional event context.</summary>
    public string? Metadata { get; init; }

    // ── Correlation / tracing ─────────────────────────────────────────────────

    public string? CorrelationId { get; init; }
    public string? RequestId { get; init; }
    public string? SessionId { get; init; }

    // ── Access control ────────────────────────────────────────────────────────

    public required VisibilityScope Visibility { get; init; }
    public required SeverityLevel Severity { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }

    // ── Integrity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// HMAC-SHA256 hash of this record's canonical fields.
    /// Only populated when QueryAuth.ExposeIntegrityHash=true.
    /// </summary>
    public string? Hash { get; init; }

    // ── Replay flag ───────────────────────────────────────────────────────────

    /// <summary>True if this record was created by an upstream replay mechanism.</summary>
    public required bool IsReplay { get; init; }

    // ── Tags ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserialized tag list from the record's TagsJson field.
    /// Empty list when no tags were provided.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
