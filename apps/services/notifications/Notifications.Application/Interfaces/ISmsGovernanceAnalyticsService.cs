namespace Notifications.Application.Interfaces;

// ─── Query types ─────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: Common date-windowed analytics query parameters.</summary>
public sealed class GovernanceAnalyticsQuery
{
    public Guid?    TenantId   { get; init; }
    public Guid?    RulePackId { get; init; }
    public Guid?    RuleId     { get; init; }
    public string?  RuleType   { get; init; }
    public string?  Severity   { get; init; }
    public DateTime? From      { get; init; }
    public DateTime? To        { get; init; }

    /// <summary>When true, include simulation evaluations. Default: true.</summary>
    public bool IncludeSimulation { get; init; } = true;

    /// <summary>When true, include live evaluations. Default: true.</summary>
    public bool IncludeLive       { get; init; } = true;

    public int Page     { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

// ─── Result types ─────────────────────────────────────────────────────────────

/// <summary>LS-NOTIF-SMS-020: Effectiveness row for a single rule.</summary>
public sealed class RuleEffectivenessRow
{
    public Guid?   RuleId       { get; init; }
    public Guid?   RulePackId   { get; init; }
    public string? RuleName     { get; init; }
    public string? RuleType     { get; init; }
    public string? Severity     { get; init; }
    public int     TotalMatches { get; init; }
    public int     BlockCount   { get; init; }
    public int     WarnCount    { get; init; }
    public int     ReviewCount  { get; init; }
    public int     AllowCount   { get; init; }
    public int     SimulationCount { get; init; }
    public int     LiveCount    { get; init; }

    /// <summary>BlockCount / TotalMatches (0.0–1.0).</summary>
    public double  BlockRate    { get; init; }

    /// <summary>ReviewCount / TotalMatches (0.0–1.0).</summary>
    public double  ReviewRate   { get; init; }

    public DateTime? LastMatchedAt { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: Time-series match count row (daily bucket).</summary>
public sealed class MatchAnalyticsRow
{
    public DateTime WindowStart  { get; init; }
    public Guid?    RuleId       { get; init; }
    public Guid?    RulePackId   { get; init; }
    public string?  RuleType     { get; init; }
    public string?  Severity     { get; init; }
    public string   DecisionType { get; init; } = string.Empty;
    public int      MatchCount   { get; init; }
    public int      SimulationCount { get; init; }
    public int      LiveCount    { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: A rule flagged as a false-positive candidate.</summary>
public sealed class FalsePositiveCandidateRow
{
    public Guid?   RuleId          { get; init; }
    public Guid?   RulePackId      { get; init; }
    public string? RuleName        { get; init; }
    public string? RuleType        { get; init; }
    public string? Severity        { get; init; }
    public int     TotalMatches    { get; init; }
    public int     WarnCount       { get; init; }
    public int     SimulationCount { get; init; }
    public int     LiveCount       { get; init; }

    /// <summary>Human-readable heuristic explanation.</summary>
    public string  Heuristic       { get; init; } = string.Empty;
    public double  FpScore         { get; init; }
}

/// <summary>LS-NOTIF-SMS-020: Effectiveness summary for a rule pack.</summary>
public sealed class PackEffectivenessRow
{
    public Guid?   RulePackId    { get; init; }
    public string? PackName      { get; init; }
    public int     ActiveRules   { get; init; }
    public int     TotalMatches  { get; init; }
    public int     BlockCount    { get; init; }
    public int     WarnCount     { get; init; }
    public int     ReviewCount   { get; init; }
    public int     AllowCount    { get; init; }
    public double  BlockRate     { get; init; }
    public DateTime? LastMatchedAt { get; init; }
}

// ─── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-020: Governance rule effectiveness analytics service.
///
/// Queries aggregate metrics from SmsGovernanceRuleMatchMetric (daily buckets).
/// Never exposes message content, raw phone numbers, or credentials.
/// Bounded date defaults prevent unbounded queries.
/// Analytics failures never impact the delivery pipeline.
/// </summary>
public interface ISmsGovernanceAnalyticsService
{
    /// <summary>Per-rule effectiveness summary across the requested window.</summary>
    Task<(IReadOnlyList<RuleEffectivenessRow> Rows, int Total)> GetRuleEffectivenessAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default);

    /// <summary>Time-series match counts per daily window bucket.</summary>
    Task<IReadOnlyList<MatchAnalyticsRow>> GetRuleMatchAnalyticsAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Rules that match false-positive heuristics:
    ///   - high warn + low block count
    ///   - high simulation count relative to live count
    ///   - recently created rules with high match rates
    /// No message content or phone numbers returned.
    /// </summary>
    Task<IReadOnlyList<FalsePositiveCandidateRow>> GetFalsePositiveCandidatesAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default);

    /// <summary>Pack-level effectiveness summary.</summary>
    Task<(IReadOnlyList<PackEffectivenessRow> Rows, int Total)> GetPackEffectivenessAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default);
}
