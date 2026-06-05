namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// Full anomaly detection response from GET /audit/analytics/anomalies.
///
/// Contains metadata about the evaluation windows and the list of firing anomalies.
/// An empty <see cref="Anomalies"/> list is a valid, normal response (no anomalies detected).
/// </summary>
public sealed class AuditAnomalyResponse
{
    // ── Evaluation metadata ───────────────────────────────────────────────────

    /// <summary>UTC timestamp when the anomaly evaluation was run.</summary>
    public required DateTimeOffset EvaluatedAt { get; init; }

    /// <summary>Start of the recent observation window (now - 24h).</summary>
    public required DateTimeOffset RecentWindowFrom { get; init; }

    /// <summary>End of the recent observation window (now).</summary>
    public required DateTimeOffset RecentWindowTo { get; init; }

    /// <summary>Start of the baseline window (now - 8d).</summary>
    public required DateTimeOffset BaselineWindowFrom { get; init; }

    /// <summary>End of the baseline window (now - 1d — excludes the recent 24h).</summary>
    public required DateTimeOffset BaselineWindowTo { get; init; }

    /// <summary>Effective tenant scope applied. Null = cross-tenant (platform admin).</summary>
    public string? EffectiveTenantId { get; init; }

    // ── Results ───────────────────────────────────────────────────────────────

    /// <summary>Total number of anomaly rules that fired.</summary>
    public required int TotalAnomalies { get; init; }

    /// <summary>
    /// Anomalies detected, ordered by severity (High first) then rule key.
    /// Empty list = no anomalies detected in the evaluated windows.
    /// </summary>
    public required IReadOnlyList<AuditAnomalyItem> Anomalies { get; init; }
}
