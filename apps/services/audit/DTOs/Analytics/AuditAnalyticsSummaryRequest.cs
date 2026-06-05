using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// Query parameters for GET /audit/analytics/summary.
///
/// Binding notes:
/// - <see cref="From"/> and <see cref="To"/> are required. The service enforces a
///   maximum window of 90 days to prevent runaway full-history scans.
/// - <see cref="TenantId"/> is ignored for tenant-scoped callers — the middleware-derived
///   tenant is substituted server-side (same pattern as the main query API).
/// - When <see cref="Category"/> is set, all sub-queries are pre-filtered to that category.
/// </summary>
public sealed class AuditAnalyticsSummaryRequest
{
    /// <summary>Start of the analytics window (inclusive, UTC).</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>End of the analytics window (exclusive, UTC).</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Restrict analytics to a specific tenant.
    /// Platform admin: optional (omit for cross-tenant). Tenant callers: ignored.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>Optional: restrict all sub-queries to a specific event category.</summary>
    public EventCategory? Category { get; set; }
}
