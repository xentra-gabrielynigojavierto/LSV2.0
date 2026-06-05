namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-019: Dynamic governance rule engine configuration options.
/// Bound from "SmsGovernanceDynamic" section in appsettings.json.
/// </summary>
public sealed class SmsGovernanceDynamicOptions
{
    public const string SectionName = "SmsGovernanceDynamic";

    /// <summary>Master kill-switch. When false, dynamic rule engine is bypassed entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, rule engine exceptions fail open (allow). Default: true.</summary>
    public bool FailOpenOnEvaluationError { get; set; } = true;

    /// <summary>Maximum pattern length in characters (for both phrases and regex). Default: 500.</summary>
    public int MaxPatternLength { get; set; } = 500;

    /// <summary>Regex evaluation timeout in milliseconds. Default: 200ms.</summary>
    public int RegexTimeoutMs { get; set; } = 200;

    /// <summary>Maximum rules evaluated per request (circuit breaker). Default: 200.</summary>
    public int MaxRulesPerEvaluation { get; set; } = 200;

    /// <summary>When true, allow decisions are also persisted (verbose audit). Default: false.</summary>
    public bool PersistAllowDecisions { get; set; } = false;

    /// <summary>When false, restricted_pattern rule creation is rejected. Default: true.</summary>
    public bool AllowRegexRules { get; set; } = true;
}
