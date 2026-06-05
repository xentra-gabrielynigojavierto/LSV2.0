using System.ComponentModel.DataAnnotations;

namespace Monitoring.Infrastructure.Scheduling;

/// <summary>
/// Strongly-typed configuration for the monitoring scheduler. Bound from
/// the <c>Monitoring:Scheduler</c> configuration section and validated at
/// startup so a misconfigured interval fails fast with a clear message
/// instead of silently behaving unpredictably.
/// </summary>
public sealed class SchedulerOptions
{
    public const string SectionName = "Monitoring:Scheduler";

    /// <summary>
    /// Whether the scheduler should tick. When <c>false</c>, the hosted
    /// service starts, logs that it is disabled, and exits without
    /// scheduling any cycles. The host (HTTP, auth, DB) is unaffected.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Wall-clock interval between cycles, in seconds. Must be at least 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage =
        "Monitoring:Scheduler:IntervalSeconds must be a positive integer.")]
    public int IntervalSeconds { get; set; } = 60;
}
