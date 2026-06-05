namespace Flow.Application.Options;

/// <summary>
/// LS-FLOW-E18 — configuration for the work-distribution intelligence
/// layer. Bound from the <c>"WorkDistribution"</c> section in
/// <c>appsettings.json</c> (or environment overrides).
///
/// <para>
/// All values have deterministic defaults so the recommendation and
/// auto-assignment features operate correctly without any configuration
/// changes. The defaults are calibrated for a typical small-to-medium
/// legal/healthcare operations team.
/// </para>
/// </summary>
public sealed class WorkDistributionOptions
{
    /// <summary>Config section key.</summary>
    public const string SectionKey = "WorkDistribution";

    // ── Capacity model ────────────────────────────────────────────

    /// <summary>
    /// Soft capacity threshold. Users at or above this count are
    /// deprioritised in recommendation output (they still appear in
    /// the candidate list but rank below users below this threshold).
    /// Default: 15.
    /// </summary>
    public int SoftCapacityThreshold { get; init; } = 15;

    /// <summary>
    /// Hard capacity maximum. Users at or above this count are
    /// considered "overloaded". The recommendation engine still
    /// returns a candidate (never blocks all recommendations) but
    /// the explanation surface will note the overload status.
    /// Default: 20.
    /// </summary>
    public int MaxActiveTasksPerUser { get; init; } = 20;

    // ── Feature flags ─────────────────────────────────────────────

    /// <summary>
    /// When <c>false</c> the recommendation endpoint returns 503
    /// Service Unavailable with a structured explanation. Useful for
    /// gradual roll-out. Default: <c>true</c>.
    /// </summary>
    public bool EnableRecommendation { get; init; } = true;

    /// <summary>
    /// When <c>false</c> the auto-assign endpoint returns 503 with a
    /// structured explanation. Default: <c>true</c>.
    /// </summary>
    public bool EnableAutoAssignment { get; init; } = true;

    // ── Candidate derivation ─────────────────────────────────────

    /// <summary>
    /// Maximum number of workload-history-derived candidates to
    /// consider when no explicit <c>candidateUserIds</c> list is
    /// supplied. Prevents a full-table-scan on tenants with very
    /// large task histories. Default: 50.
    /// </summary>
    public int MaxDerivedCandidates { get; init; } = 50;
}
