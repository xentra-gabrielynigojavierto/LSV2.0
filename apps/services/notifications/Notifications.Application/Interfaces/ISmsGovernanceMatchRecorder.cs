namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-020: Fire-and-forget recorder for governance rule match metrics.
///
/// Called from SmsGovernanceRuleEngine after each evaluation.
/// Must never throw — failures are swallowed so delivery is not impacted.
/// Aggregates into SmsGovernanceRuleMatchMetric daily buckets (upsert pattern).
///
/// NEVER records message content, raw phone numbers, or credentials.
/// </summary>
public interface ISmsGovernanceMatchRecorder
{
    /// <summary>
    /// Record matched rules from an evaluation result.
    /// isDryRun=true increments SimulationCount; false increments LiveCount.
    /// Called fire-and-forget — implementation must not throw.
    /// </summary>
    void RecordMatches(
        SmsGovernanceRuleEvaluationResult result,
        Guid? tenantId,
        bool isDryRun);
}
