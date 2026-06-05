using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Ingest;

/// <summary>
/// Canonical ingest contract for a single audit event.
///
/// Design intent:
/// - Nested objects (Scope, Actor, Entity) keep the top-level shape clean while
///   carrying rich context without flattening everything into a single wide struct.
/// - String JSON fields (Before, After, Metadata) are schema-agnostic; the audit
///   service stores them verbatim without interpreting structure.
/// - Tags carries an open list of string labels for ad-hoc grouping; no taxonomy enforced.
/// - The shape is stable by design — future subscriber contracts (webhooks, event bus)
///   can receive this DTO as-is without a breaking change.
/// </summary>
public sealed class IngestAuditEventRequest
{
    // ── Event identity ────────────────────────────────────────────────────────

    /// <summary>
    /// Source-assigned domain event identifier. Optional.
    /// Not used for deduplication — use IdempotencyKey for that.
    /// </summary>
    public Guid? EventId { get; set; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>
    /// Dot-notation event code. Convention: {domain}.{resource}.{verb}.
    /// Examples: "user.login.succeeded", "document.uploaded", "role.assigned".
    /// Required. Max 200 chars.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Broad domain classification. Drives retention policy and dashboard routing.
    /// Required. Defaults to Business if omitted.
    /// </summary>
    public EventCategory EventCategory { get; set; } = EventCategory.Business;

    // ── Source provenance ─────────────────────────────────────────────────────

    /// <summary>
    /// Logical name of the system producing this event. Required. Max 200 chars.
    /// Examples: "identity-service", "care-connect", "fund-service".
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Sub-component or microservice within SourceSystem. Optional.
    /// </summary>
    public string? SourceService { get; set; }

    /// <summary>
    /// Deployment environment of the source system. Optional.
    /// Examples: "production", "staging", "dev".
    /// </summary>
    public string? SourceEnvironment { get; set; }

    // ── Scope / tenancy ───────────────────────────────────────────────────────

    /// <summary>
    /// Tenancy and organizational scope. Required.
    /// At minimum, set ScopeType. Populate TenantId/OrganizationId/UserId
    /// according to the ScopeType value.
    /// </summary>
    public AuditEventScopeDto Scope { get; set; } = new();

    // ── Actor ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The principal that performed the action. Required.
    /// At minimum, set Type. Set Id and Name when identity is known.
    /// </summary>
    public AuditEventActorDto Actor { get; set; } = new();

    // ── Target entity ─────────────────────────────────────────────────────────

    /// <summary>
    /// The resource that was acted upon. Optional.
    /// Omit for non-resource-targeted events (e.g. system startup, login attempt).
    /// </summary>
    public AuditEventEntityDto? Entity { get; set; }

    // ── Action description ────────────────────────────────────────────────────

    /// <summary>
    /// Verb describing what was done. Convention: PascalCase.
    /// Examples: "Created", "Updated", "Deleted", "Approved", "LoginAttempted".
    /// Required. Max 200 chars.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable summary for audit log displays. Required. Max 2000 chars.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // ── State snapshots ───────────────────────────────────────────────────────

    /// <summary>
    /// JSON snapshot of the resource state before the action.
    /// Null for creation events or events with no prior state.
    /// Stored verbatim — no schema validation performed by the audit service.
    /// </summary>
    public string? Before { get; set; }

    /// <summary>
    /// JSON snapshot of the resource state after the action.
    /// Null for deletion events or events with no resulting state.
    /// Stored verbatim — no schema validation performed by the audit service.
    /// </summary>
    public string? After { get; set; }

    // ── Extended context ──────────────────────────────────────────────────────

    /// <summary>
    /// Arbitrary JSON object for additional context that doesn't fit canonical fields.
    /// Stored verbatim. Consumers should parse defensively.
    /// </summary>
    public string? Metadata { get; set; }

    // ── Correlation / tracing ─────────────────────────────────────────────────

    /// <summary>
    /// Distributed trace correlation ID. Typically from W3C traceparent or X-Correlation-Id.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// HTTP request identifier from the originating request.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// User session identifier, if the action occurred within a session context.
    /// </summary>
    public string? SessionId { get; set; }

    // ── Access control ────────────────────────────────────────────────────────

    /// <summary>
    /// Visibility scope controlling who can retrieve this record via the query API.
    /// Defaults to Tenant — visible to tenant admins and above.
    /// </summary>
    public VisibilityScope Visibility { get; set; } = VisibilityScope.Tenant;

    /// <summary>
    /// Operational severity at the time the event occurred.
    /// Defaults to Info for successful operations.
    /// </summary>
    public SeverityLevel Severity { get; set; } = SeverityLevel.Info;

    // ── Timestamps ────────────────────────────────────────────────────────────

    /// <summary>
    /// UTC time the event occurred in the source system.
    /// If null, the audit service uses the server receipt time.
    /// </summary>
    public DateTimeOffset? OccurredAtUtc { get; set; }

    // ── Deduplication / replay ────────────────────────────────────────────────

    /// <summary>
    /// Source-provided idempotency key for deduplication.
    /// The ingest pipeline will reject a second submission with the same key
    /// within the configured dedup window. Optional but strongly recommended
    /// for retry-safe ingestion from reliable event producers.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Set to true when re-submitting an event through an upstream replay mechanism.
    /// The original record is preserved; this creates a new record marked as a replay.
    /// Defaults to false.
    /// </summary>
    public bool IsReplay { get; set; } = false;

    // ── Tags ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional list of string labels for ad-hoc grouping and filtering.
    /// No taxonomy enforced. Examples: ["pii", "gdpr", "high-risk"].
    /// Max 20 tags; max 100 chars per tag.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }
}
