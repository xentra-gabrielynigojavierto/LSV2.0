namespace PlatformAuditEventService.Models;

/// <summary>
/// Canonical, immutable audit/event record persisted by the Platform Audit/Event Service.
/// All fields are set at ingestion time and must not be mutated after write.
/// </summary>
public sealed record AuditEvent
{
    public Guid   Id              { get; init; } = Guid.NewGuid();

    /// <summary>Originating system or application (e.g. "identity-service", "fund-service").</summary>
    public string Source          { get; init; } = string.Empty;

    /// <summary>Event type code (e.g. "user.login", "document.uploaded", "role.assigned").</summary>
    public string EventType       { get; init; } = string.Empty;

    /// <summary>High-level category (e.g. "security", "access", "business", "admin", "system").</summary>
    public string Category        { get; init; } = string.Empty;

    /// <summary>Severity level: DEBUG | INFO | WARN | ERROR | CRITICAL.</summary>
    public string Severity        { get; init; } = "INFO";

    /// <summary>Tenant identifier. Null for platform-level events.</summary>
    public string? TenantId       { get; init; }

    /// <summary>Acting user or service principal that triggered the event.</summary>
    public string? ActorId        { get; init; }

    /// <summary>Human-readable actor display label (e.g. email, service name).</summary>
    public string? ActorLabel     { get; init; }

    /// <summary>Target resource type affected (e.g. "User", "Document", "Application").</summary>
    public string? TargetType     { get; init; }

    /// <summary>Target resource identifier.</summary>
    public string? TargetId       { get; init; }

    /// <summary>Human-readable description of the event.</summary>
    public string  Description    { get; init; } = string.Empty;

    /// <summary>Outcome of the action: SUCCESS | FAILURE | PARTIAL | UNKNOWN.</summary>
    public string  Outcome        { get; init; } = "SUCCESS";

    /// <summary>Client IP address, if available.</summary>
    public string? IpAddress      { get; init; }

    /// <summary>User-agent string, if available.</summary>
    public string? UserAgent      { get; init; }

    /// <summary>Correlation/trace identifier for distributed request tracing.</summary>
    public string? CorrelationId  { get; init; }

    /// <summary>Arbitrary structured metadata as JSON string.</summary>
    public string? Metadata       { get; init; }

    /// <summary>UTC timestamp when the event occurred in the originating system.</summary>
    public DateTimeOffset OccurredAtUtc  { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when this record was ingested and persisted.</summary>
    public DateTimeOffset IngestedAtUtc  { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>HMAC-SHA256 hash over canonical fields for tamper-evidence.</summary>
    public string? IntegrityHash  { get; init; }
}
