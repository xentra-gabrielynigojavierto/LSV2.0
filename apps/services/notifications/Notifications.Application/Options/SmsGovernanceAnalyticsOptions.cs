namespace Notifications.Application.Options;

/// <summary>LS-NOTIF-SMS-020: Configuration for governance rule effectiveness analytics.</summary>
public sealed class SmsGovernanceAnalyticsOptions
{
    public const string SectionName = "SmsGovernanceAnalytics";

    /// <summary>Master switch. When false, match recording is a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default date window in days for analytics queries (bounded default).</summary>
    public int WindowDays { get; set; } = 30;

    /// <summary>Maximum rows returned by any analytics endpoint.</summary>
    public int MaxResultRows { get; set; } = 200;

    /// <summary>
    /// Minimum warn count for a rule to be considered a false-positive candidate.
    /// </summary>
    public int FalsePositiveWarnThreshold { get; set; } = 10;

    /// <summary>
    /// Maximum live/simulation ratio below which a rule is a false-positive candidate.
    /// 0.1 = fewer than 10% of matches are live evaluations.
    /// </summary>
    public double FalsePositiveLiveToSimRatio { get; set; } = 0.1;
}
