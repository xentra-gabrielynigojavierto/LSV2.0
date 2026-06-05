using Monitoring.Domain.Monitoring;

namespace Monitoring.Application.Scheduling;

/// <summary>
/// Evaluates alert rules for a single entity's freshly-produced
/// <see cref="CheckResult"/> and persists / updates alert records as
/// rules dictate.
///
/// <para><b>Where it runs</b>: invoked by the cycle executor between
/// the history-row write and the current-status upsert. That ordering
/// lets the engine read the prior <c>EntityCurrentStatus</c> via a
/// normal EF read before the upsert overwrites it.</para>
///
/// <para><b>Failure surfacing</b>: implementations do not catch their
/// own exceptions; the cycle executor wraps each call in its own
/// per-entity try/catch so one bad alert evaluation never breaks the
/// cycle and never prevents the current-status upsert from running.</para>
///
/// <para><b>Scope (this feature)</b>: only the transition-into-Down
/// rule with active-row dedup, plus minimal Down→Up/Unknown
/// resolution. Notifications, ack, escalations, and search APIs are
/// future features.</para>
/// </summary>
public interface IAlertRuleEngine
{
    /// <summary>
    /// Evaluates all configured rules for <paramref name="entity"/>
    /// against <paramref name="result"/> and writes/updates alert rows
    /// as needed. Idempotent across repeated identical inputs (dedup).
    /// </summary>
    Task EvaluateAsync(
        MonitoredEntity entity,
        CheckResult result,
        CancellationToken cancellationToken);
}
