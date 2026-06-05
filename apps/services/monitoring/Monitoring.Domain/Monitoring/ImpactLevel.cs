namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Describes how impactful a monitored entity's failure or degradation is to
/// the LegalSynq platform. Drives later alerting/summarization (out of scope
/// here) — this enum only carries the semantic classification.
/// </summary>
public enum ImpactLevel
{
    /// <summary>Failure blocks core platform functionality.</summary>
    Blocking = 1,

    /// <summary>Failure degrades the platform but core flows still work.</summary>
    Degraded = 2,

    /// <summary>Failure has no material user-facing impact.</summary>
    Optional = 3,
}
