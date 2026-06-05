using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Entities;

/// <summary>
/// Canonical append-only persistence model for a single auditable event.
///
/// Design principles:
/// - Append-only: records are never updated or deleted by the ingest path.
/// - Cryptographic chaining: <see cref="Hash"/> covers this record's canonical fields;
///   <see cref="PreviousHash"/> links to the preceding record in the same scope chain,
///   enabling tamper-evidence without a full blockchain dependency.
/// - Scope isolation: ScopeType + TenantId + OrganizationId + UserScopeId collectively
///   determine row-level access; no single field is sufficient alone.
/// - JSON columns (BeforeJson, AfterJson, MetadataJson, TagsJson) carry structured
///   payloads but are stored as text to remain schema-agnostic.
/// - No soft-delete: IsReplay marks re-submitted events without removing originals.
/// </summary>
public sealed class AuditEventRecord
{
    // ── Primary key ──────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-increment surrogate key. Used for ordered pagination and chaining.
    /// Not exposed externally — use <see cref="AuditId"/> as the public identifier.
    /// </summary>
    public long Id { get; init; }

    // ── Business identifiers ─────────────────────────────────────────────────

    /// <summary>
    /// Platform-assigned stable public identifier for this record (UUIDv7 recommended
    /// for time-ordered inserts). Exposed in API responses and exports.
    /// </summary>
    public required Guid AuditId { get; init; }

    /// <summary>
    /// Source-assigned identifier for the domain event that triggered this record.
    /// May be shared across retries/replays; use <see cref="IdempotencyKey"/> for
    /// dedup logic instead of relying on this field alone.
    /// </summary>
    public Guid? EventId { get; init; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>
    /// Dot-notation event code from the source system. Convention: {domain}.{resource}.{verb}.
    /// Examples: "user.login.succeeded", "document.uploaded", "role.assigned".
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Broad domain classification used for retention policy selection and dashboards.
    /// </summary>
    public required EventCategory EventCategory { get; init; }

    // ── Source provenance ─────────────────────────────────────────────────────

    /// <summary>
    /// Logical system name that produced this event. Typically matches a service name
    /// or product identifier. Example: "identity-service", "care-connect".
    /// </summary>
    public required string SourceSystem { get; init; }

    /// <summary>
    /// Specific microservice or component within <see cref="SourceSystem"/>.
    /// Allows distinguishing between sub-components that share a system name.
    /// </summary>
    public string? SourceService { get; init; }

    /// <summary>
    /// Deployment environment tag from the source. Example: "production", "staging", "dev".
    /// Populated from the source system's own environment label, not this service's env.
    /// </summary>
    public string? SourceEnvironment { get; init; }

    // ── Scope / tenancy ───────────────────────────────────────────────────────

    /// <summary>
    /// Platform-level installation or partition identifier. Null for single-platform deployments.
    /// </summary>
    public Guid? PlatformId { get; init; }

    /// <summary>
    /// Top-level tenancy boundary. Required when <see cref="ScopeType"/> is Tenant, Organization, or User.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Organization within a tenant. Required when <see cref="ScopeType"/> is Organization.
    /// </summary>
    public string? OrganizationId { get; init; }

    /// <summary>
    /// User-level scope identifier. Required when <see cref="ScopeType"/> is User.
    /// Typically matches <see cref="ActorId"/> but may differ for impersonation scenarios.
    /// </summary>
    public string? UserScopeId { get; init; }

    /// <summary>
    /// Organizational level this record is scoped to. Drives multi-tenancy isolation.
    /// </summary>
    public required ScopeType ScopeType { get; init; }

    // ── Actor / identity ──────────────────────────────────────────────────────

    /// <summary>
    /// Stable identifier of the principal that performed the action.
    /// Format is source-system-specific (user GUID, service name, etc.).
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Classification of the principal kind. Determines how <see cref="ActorId"/> should be resolved.
    /// </summary>
    public required ActorType ActorType { get; init; }

    /// <summary>
    /// Display name of the actor at the time of the event. Snapshot — not updated if actor name changes.
    /// </summary>
    public string? ActorName { get; init; }

    /// <summary>
    /// IP address from which the action was initiated. IPv4 or IPv6. Max 45 chars.
    /// </summary>
    public string? ActorIpAddress { get; init; }

    /// <summary>
    /// User-Agent string from the originating HTTP request, if available.
    /// </summary>
    public string? ActorUserAgent { get; init; }

    // ── Target entity ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resource type that was acted upon. Convention: PascalCase domain model name.
    /// Examples: "User", "Document", "Appointment", "Role".
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>
    /// Identifier of the resource that was acted upon.
    /// </summary>
    public string? EntityId { get; init; }

    // ── Action payload ────────────────────────────────────────────────────────

    /// <summary>
    /// Verb describing what was done. Convention: PascalCase.
    /// Examples: "Created", "Updated", "Deleted", "Approved", "LoginAttempted".
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Human-readable summary of the event for audit log displays. Required; max 2000 chars.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON snapshot of the resource state before the action. Null for creation events.
    /// Stored as raw text; no schema enforced at the audit layer.
    /// </summary>
    public string? BeforeJson { get; init; }

    /// <summary>
    /// JSON snapshot of the resource state after the action. Null for deletion events.
    /// Stored as raw text; no schema enforced at the audit layer.
    /// </summary>
    public string? AfterJson { get; init; }

    /// <summary>
    /// Arbitrary JSON object for additional context that does not fit the canonical fields.
    /// Stored as raw text. Consumers should parse defensively.
    /// </summary>
    public string? MetadataJson { get; init; }

    // ── Correlation / tracing ─────────────────────────────────────────────────

    /// <summary>
    /// Distributed trace correlation identifier. Used to link events across services.
    /// Typically populated from the W3C traceparent / X-Correlation-Id header.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// HTTP request identifier from the originating request, if available.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// User session identifier, if the event originated within a session context.
    /// </summary>
    public string? SessionId { get; init; }

    // ── Access control ────────────────────────────────────────────────────────

    /// <summary>
    /// Controls which roles can retrieve this record via the query API.
    /// Set by the ingest source; defaulted by category conventions if omitted.
    /// </summary>
    public required VisibilityScope VisibilityScope { get; init; }

    /// <summary>
    /// Operational severity of the event at the time it occurred.
    /// </summary>
    public required SeverityLevel Severity { get; init; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>
    /// When the event happened in the source system. If not provided by the source,
    /// defaults to <see cref="RecordedAtUtc"/>. Always stored in UTC.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// When this record was written into the audit store. Set by the ingest pipeline.
    /// Always stored in UTC. Immutable after creation.
    /// </summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    // ── Integrity chain ───────────────────────────────────────────────────────

    /// <summary>
    /// HMAC-SHA256 hash computed over the canonical fields of this record.
    /// Canonical field set: AuditId, EventType, SourceSystem, TenantId, ActorId,
    /// EntityType, EntityId, Action, OccurredAtUtc, RecordedAtUtc.
    /// Null when integrity signing is disabled (Integrity:HmacKeyBase64 is empty).
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Hash of the immediately preceding record in the same (TenantId, SourceSystem) chain.
    /// Enables sequential tamper detection without a global chain. Null for the first record
    /// in a chain or when chaining is disabled.
    /// </summary>
    public string? PreviousHash { get; init; }

    // ── Deduplication / replay ────────────────────────────────────────────────

    /// <summary>
    /// Source-provided idempotency key. The ingest pipeline rejects a second submission
    /// with the same key within the configured dedup window. Distinct from <see cref="EventId"/>,
    /// which is a domain event identifier and is not guaranteed unique on retries.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// True when this record was re-submitted from an upstream event replay mechanism.
    /// The original record is preserved; this is an additional record, not a replacement.
    /// </summary>
    public required bool IsReplay { get; init; }

    // ── Tags ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON array of string tags for ad-hoc grouping and filtering.
    /// Example: ["pii", "gdpr", "high-risk"]. Max 100 chars per tag.
    /// Stored as raw JSON text.
    /// </summary>
    public string? TagsJson { get; init; }
}
