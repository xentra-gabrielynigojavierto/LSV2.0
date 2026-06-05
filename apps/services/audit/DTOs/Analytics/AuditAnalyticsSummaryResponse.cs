namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// Full analytics summary returned by GET /audit/analytics/summary.
///
/// All sub-collections are non-null. <see cref="TopTenants"/> is null when
/// the caller does not have platform-admin scope (tenant isolation enforcement).
/// </summary>
public sealed class AuditAnalyticsSummaryResponse
{
    // ── Window metadata ───────────────────────────────────────────────────────

    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To   { get; init; }

    /// <summary>Effective tenant scope applied (null = cross-tenant / platform-wide).</summary>
    public string? EffectiveTenantId { get; init; }

    // ── KPI scalars ───────────────────────────────────────────────────────────

    /// <summary>Total events in the window matching scope/category filters.</summary>
    public required long TotalEvents { get; init; }

    /// <summary>Events with EventCategory = Security in the window.</summary>
    public required long SecurityEventCount { get; init; }

    /// <summary>
    /// Events whose EventType contains ".denied" or ".deny" — a denial-signal count.
    /// This is a rough operational metric; not a formal audit of all access denials.
    /// </summary>
    public required long DenialEventCount { get; init; }

    /// <summary>
    /// Governance events: legal holds placed/released, integrity checkpoints generated,
    /// and audit exports initiated. Identified by EventType prefix patterns.
    /// </summary>
    public required long GovernanceEventCount { get; init; }

    // ── Time-series ───────────────────────────────────────────────────────────

    /// <summary>Event count per calendar day (UTC date), ordered chronologically.</summary>
    public required IReadOnlyList<AuditVolumeByDayItem> VolumeByDay { get; init; }

    // ── Breakdowns ────────────────────────────────────────────────────────────

    /// <summary>Event count per EventCategory, ordered by count descending.</summary>
    public required IReadOnlyList<AuditCategoryBreakdownItem> ByCategory { get; init; }

    /// <summary>Event count per SeverityLevel, ordered by severity ascending.</summary>
    public required IReadOnlyList<AuditSeverityBreakdownItem> BySeverity { get; init; }

    // ── Top-N tables ──────────────────────────────────────────────────────────

    /// <summary>Top 15 event types by count, ordered by count descending.</summary>
    public required IReadOnlyList<AuditTopEventTypeItem> TopEventTypes { get; init; }

    /// <summary>Top 10 actors by event count, ordered by count descending.</summary>
    public required IReadOnlyList<AuditTopActorItem> TopActors { get; init; }

    /// <summary>
    /// Top 10 tenants by event count. Null when the caller is not platform admin
    /// (tenant-scoped callers must not see cross-tenant counts).
    /// </summary>
    public IReadOnlyList<AuditTopTenantItem>? TopTenants { get; init; }
}
