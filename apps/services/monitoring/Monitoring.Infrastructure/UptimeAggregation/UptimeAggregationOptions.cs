using System.ComponentModel.DataAnnotations;

namespace Monitoring.Infrastructure.UptimeAggregation;

/// <summary>
/// Configuration for the uptime aggregation hosted service.
/// Bound from the <c>Monitoring:UptimeAggregation</c> config section.
/// </summary>
public sealed class UptimeAggregationOptions
{
    public const string SectionName = "Monitoring:UptimeAggregation";

    /// <summary>
    /// How often the aggregation engine runs, in seconds. Default: 300 (5 minutes).
    /// </summary>
    [Range(10, 3600, ErrorMessage = "Monitoring:UptimeAggregation:IntervalSeconds must be between 10 and 3600.")]
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>
    /// How many days back the aggregation engine covers. Default: 91 days (covers the 90d window).
    /// </summary>
    [Range(1, 365, ErrorMessage = "Monitoring:UptimeAggregation:LookbackDays must be between 1 and 365.")]
    public int LookbackDays { get; set; } = 91;

    /// <summary>
    /// Whether the aggregation engine is enabled. Set to false to disable without removing the service.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
