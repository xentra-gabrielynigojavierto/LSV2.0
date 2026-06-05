namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-020: Aggregate match metrics for governance rule effectiveness analytics.
///
/// One row per (RuleId, RulePackId, TenantId, RuleType, Severity, WindowStart) daily bucket.
/// Counts are upserted after each evaluation — never raw message content or phone numbers.
/// Simulation and live evaluations are tracked separately.
///
/// WindowStart/WindowEnd = UTC day boundaries (00:00:00 / 23:59:59).
/// </summary>
public sealed class SmsGovernanceRuleMatchMetric
{
    public Guid     Id              { get; set; } = Guid.NewGuid();

    /// <summary>null for pack-level-only records.</summary>
    public Guid?    RuleId          { get; set; }

    public Guid?    RulePackId      { get; set; }

    /// <summary>null for global/platform-wide aggregations.</summary>
    public Guid?    TenantId        { get; set; }

    public string?  RuleType        { get; set; }
    public string?  Severity        { get; set; }

    /// <summary>Dominant decision: allow | warn | review_required | block | override_allowed</summary>
    public string   DecisionType    { get; set; } = string.Empty;

    public string?  ReasonCode      { get; set; }

    public int      MatchCount      { get; set; }
    public int      BlockCount      { get; set; }
    public int      WarnCount       { get; set; }
    public int      ReviewCount     { get; set; }
    public int      AllowCount      { get; set; }

    /// <summary>Evaluations triggered from simulation (IsDryRun=true).</summary>
    public int      SimulationCount { get; set; }

    /// <summary>Evaluations triggered from live delivery pipeline.</summary>
    public int      LiveCount       { get; set; }

    /// <summary>UTC start of the daily aggregation window (midnight).</summary>
    public DateTime WindowStart     { get; set; }

    /// <summary>UTC end of the daily aggregation window (23:59:59.999).</summary>
    public DateTime WindowEnd       { get; set; }

    public DateTime? LastMatchedAt  { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt       { get; set; } = DateTime.UtcNow;
}
