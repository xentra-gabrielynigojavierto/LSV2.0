namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Current operational status of a monitored entity, derived from its
/// most recent <see cref="CheckResult"/>-equivalent execution outcome.
///
/// <para>This enum is the single source of truth for current-state values
/// and is shared by the evaluator (which produces them), the durable
/// current-state row (which stores them), and any future read API
/// (which serializes them).</para>
///
/// <para>Numeric values are explicit so the enum stays stable across
/// reorderings; <see cref="Unknown"/> uses 99 to mirror the
/// "<c>Other = 99</c>" convention used by other domain enums and to
/// reserve the low numbers for primary states.</para>
///
/// <para><b>Note on <see cref="Degraded"/></b>: it is part of the model
/// for forward compatibility (latency soft-failures, partial multi-target
/// failures, smoothing rules) but the current evaluation rules in
/// <see cref="StatusEvaluator"/> do not assign it. Adding it later will
/// not require a schema or API break.</para>
/// </summary>
public enum EntityStatus
{
    /// <summary>Latest result indicates a successful check.</summary>
    Up = 1,

    /// <summary>Latest result indicates a clear failed check.</summary>
    Down = 2,

    /// <summary>Reserved for partial / soft-failure classification.
    /// Not assigned by current rules — see <see cref="StatusEvaluator"/>.</summary>
    Degraded = 3,

    /// <summary>No current result, or insufficient basis to evaluate.
    /// Includes the "skipped by adapter" and "no row yet" cases.</summary>
    Unknown = 99,
}
