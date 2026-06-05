namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// Payload for the "audit record ingested" integration event.
///
/// This is the downstream-facing contract published to brokers or internal
/// channels when a new audit record is successfully persisted.
///
/// Design principles:
///   1. <b>No internal audit-store fields.</b>
///      <c>Hash</c>, <c>PreviousHash</c>, <c>BeforeJson</c>, <c>AfterJson</c>,
///      and <c>Tags</c> are omitted. These are integrity and audit-store implementation
///      details that downstream consumers have no business need for, and hash values
///      must not be exfiltrated through the message bus.
///
///   2. <b>Flat structure.</b>
///      No nested objects — only primitives and string representations of enums.
///      This makes the payload trivially serialisable by any JSON library without
///      custom converters or namespace dependencies.
///
///   3. <b>String enums.</b>
///      <c>EventCategory</c>, <c>Severity</c>, and <c>ActorType</c> are serialised
///      as their string names (e.g. "Security", "Warning", "User"), not integers.
///      Consumers can evolve independently without being coupled to enum ordinals.
///
///   4. <b>Stable across versions.</b>
///      Add new fields in a backwards-compatible way (nullable, with defaults).
///      Increment <see cref="IntegrationEvent{TPayload}.SchemaVersion"/> when making
///      breaking changes. Never remove or rename existing fields without a versioning
///      strategy.
///
/// Consumers who need the full audit record (including state snapshots or hash chain)
/// should call <c>GET /audit/events/{auditId}</c> on the audit service directly.
/// </summary>
public sealed class AuditRecordIntegrationEvent
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>The stable audit record identifier assigned by the audit service.</summary>
    public required Guid AuditId { get; init; }

    // ── Event classification ──────────────────────────────────────────────────

    /// <summary>
    /// Free-form event type string from the source system.
    /// Example: "user.login.succeeded", "document.signed", "consent.revoked".
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Category name string (from <see cref="Enums.EventCategory"/>).
    /// Example: "Security", "Compliance", "System".
    /// </summary>
    public required string EventCategory { get; init; }

    /// <summary>
    /// Severity level string (from <see cref="Enums.SeverityLevel"/>).
    /// Example: "Info", "Warning", "Error", "Critical".
    /// </summary>
    public required string Severity { get; init; }

    // ── Source ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Originating microservice identifier.
    /// Example: "identity-service", "fund-service".
    /// </summary>
    public required string SourceSystem { get; init; }

    // ── Scope ─────────────────────────────────────────────────────────────────

    /// <summary>The tenant this event belongs to. Null for platform-level events.</summary>
    public string? TenantId { get; init; }

    /// <summary>The organization this event belongs to. Null when not applicable.</summary>
    public string? OrganizationId { get; init; }

    // ── Actor ─────────────────────────────────────────────────────────────────

    /// <summary>Identifier of the principal who performed the action. Null for system actors.</summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Actor type string (from <see cref="Enums.ActorType"/>).
    /// Example: "User", "Service", "System".
    /// </summary>
    public required string ActorType { get; init; }

    // ── Target entity ─────────────────────────────────────────────────────────

    /// <summary>Type of the entity acted upon. Example: "User", "Document", "FundAccount".</summary>
    public string? EntityType { get; init; }

    /// <summary>Identifier of the entity acted upon. Null when not applicable.</summary>
    public string? EntityId { get; init; }

    // ── Action ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The action that occurred.
    /// Example: "LoginSucceeded", "DocumentSigned", "ConsentRevoked".
    /// </summary>
    public required string Action { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>When the event actually occurred in the source system (UTC).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>When the audit record was written to the audit store (UTC).</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    // ── Tracing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional correlation ID for distributed tracing.
    /// Propagated from the originating ingest request.
    /// </summary>
    public string? CorrelationId { get; init; }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>
    /// When true, this record is a replay of a prior event (e.g. during migration).
    /// Consumers should decide whether to act on replayed events based on their own
    /// idempotency strategy.
    /// </summary>
    public required bool IsReplay { get; init; }
}
