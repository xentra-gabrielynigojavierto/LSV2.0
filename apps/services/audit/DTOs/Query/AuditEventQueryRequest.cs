using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Query;

/// <summary>
/// Query filter and pagination parameters for listing audit event records.
///
/// Binding notes:
/// - Designed for query-string binding (HTTP GET). All fields are nullable/optional.
/// - TenantId is the primary isolation boundary. When QueryAuth.EnforceTenantScope=true,
///   the middleware overrides TenantId with the caller's claim regardless of what is
///   sent in the request.
/// - MinSeverity/MaxSeverity use the numeric ordering of SeverityLevel to form a range.
/// - EventTypes accepts a comma-delimited list for multi-value filtering.
/// - SortBy accepts a fixed set of field names; unrecognized values fall back to OccurredAtUtc.
/// </summary>
public sealed class AuditEventQueryRequest
{
    // ── Scope filters ─────────────────────────────────────────────────────────

    /// <summary>
    /// Restrict results to a specific tenant. Overridden by middleware when
    /// QueryAuth.EnforceTenantScope is true.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Further restrict to a specific organization within the tenant.
    /// </summary>
    public string? OrganizationId { get; set; }

    // ── Classification filters ────────────────────────────────────────────────

    /// <summary>
    /// Filter by event category. Null returns all categories.
    /// </summary>
    public EventCategory? Category { get; set; }

    /// <summary>
    /// Return only events at or above this severity level.
    /// Uses SeverityLevel's numeric ordering (Info=2, Warn=4, etc.).
    /// </summary>
    public SeverityLevel? MinSeverity { get; set; }

    /// <summary>
    /// Return only events at or below this severity level.
    /// </summary>
    public SeverityLevel? MaxSeverity { get; set; }

    /// <summary>
    /// Filter to specific event type codes. Accepts exact dot-notation codes.
    /// Multiple values are OR-ed: a result matches if its EventType equals any entry.
    /// </summary>
    public IReadOnlyList<string>? EventTypes { get; set; }

    /// <summary>
    /// Filter by source system name. Exact match.
    /// </summary>
    public string? SourceSystem { get; set; }

    /// <summary>
    /// Filter by source service name. Exact match.
    /// </summary>
    public string? SourceService { get; set; }

    // ── Actor / identity filters ──────────────────────────────────────────────

    /// <summary>
    /// Filter to events performed by a specific actor.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Filter to a specific actor type.
    /// </summary>
    public ActorType? ActorType { get; set; }

    // ── Entity / resource filters ─────────────────────────────────────────────

    /// <summary>
    /// Filter to events that targeted a specific resource type.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Filter to events that targeted a specific resource ID.
    /// Best combined with EntityType.
    /// </summary>
    public string? EntityId { get; set; }

    // ── Correlation filters ───────────────────────────────────────────────────

    /// <summary>
    /// Return all events that share a correlation ID (cross-service trace).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Return all events that share a session ID.
    /// </summary>
    public string? SessionId { get; set; }

    // ── Time range ────────────────────────────────────────────────────────────

    /// <summary>
    /// Return events that occurred at or after this UTC timestamp (inclusive).
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Return events that occurred before this UTC timestamp (exclusive).
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Filter by source environment label. Exact match.
    /// Example: "production", "staging".
    /// </summary>
    public string? SourceEnvironment { get; set; }

    // ── Correlation filters ───────────────────────────────────────────────────

    /// <summary>
    /// Return all events that share an HTTP request ID.
    /// </summary>
    public string? RequestId { get; set; }

    // ── Visibility ────────────────────────────────────────────────────────────

    /// <summary>
    /// Restrict results to records with VisibilityScope at or below this level.
    /// Callers without elevated roles should not receive Platform-scoped records.
    /// Enforced in combination with QueryAuth role claims.
    /// </summary>
    public VisibilityScope? MaxVisibility { get; set; }

    /// <summary>
    /// Exact visibility scope filter. When set, only records with this exact
    /// <see cref="VisibilityScope"/> value are returned.
    /// Takes precedence over <see cref="MaxVisibility"/> when both are provided.
    /// </summary>
    public VisibilityScope? Visibility { get; set; }

    // ── Text search ───────────────────────────────────────────────────────────

    /// <summary>
    /// Substring search within the Description field (case-insensitive).
    /// Expensive on large datasets — use with a time range when possible.
    /// </summary>
    public string? DescriptionContains { get; set; }

    // ── Pagination ────────────────────────────────────────────────────────────

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of records per page. Capped at QueryAuth.MaxPageSize (default 500).
    /// Defaults to 50.
    /// </summary>
    public int PageSize { get; set; } = 50;

    // ── Sorting ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Field to sort by. Accepted values: "occurredAtUtc" | "recordedAtUtc" | "severity".
    /// Unrecognized values fall back to "occurredAtUtc".
    /// </summary>
    public string SortBy { get; set; } = "occurredAtUtc";

    /// <summary>
    /// True for newest-first ordering. Defaults to true.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
