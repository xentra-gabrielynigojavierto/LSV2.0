namespace Notifications.Application.Options;

/// <summary>LS-NOTIF-SMS-017: SMS governance configuration options.</summary>
public sealed class SmsGovernanceOptions
{
    public const string SectionName = "SmsGovernance";

    /// <summary>Master switch. When false, all governance evaluations return allow immediately.</summary>
    public bool   Enabled                   { get; set; } = true;

    /// <summary>Default timezone when a policy does not specify one (IANA or Windows tz name).</summary>
    public string DefaultTimezone           { get; set; } = "UTC";

    /// <summary>When true, governance decisions are persisted for audit. When false, only blocking decisions are persisted.</summary>
    public bool   DecisionAuditEnabled      { get; set; } = true;

    /// <summary>
    /// When true (default): evaluation exceptions return allow.
    /// When false: evaluation exceptions return block (fail-closed, stricter).
    /// </summary>
    public bool   FailOpenOnEvaluationError { get; set; } = true;

    /// <summary>Max milliseconds allowed for policy evaluation before timing out (informational; not enforced via hard timeout).</summary>
    public int    MaxPolicyEvaluationMs     { get; set; } = 100;

    /// <summary>Default window used for rate limit count queries when policy does not specify.</summary>
    public int    RateLimitWindowMinutes    { get; set; } = 60;

    /// <summary>When true, emergency override is a recognised decision type. Otherwise EmergencyOverride requests degrade to normal evaluation.</summary>
    public bool   EmergencyOverrideEnabled  { get; set; } = false;
}
