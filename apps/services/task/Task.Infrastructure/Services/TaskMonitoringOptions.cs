namespace Task.Infrastructure.Services;

/// <summary>
/// TASK-B05 (TASK-015) — bind from the <c>MonitoringService</c> configuration section.
/// When <see cref="BaseUrl"/> is empty the registrar is silently skipped (dev mode).
/// </summary>
public sealed class TaskMonitoringOptions
{
    public const string SectionName = "MonitoringService";

    /// <summary>Base URL of the Monitoring service, e.g. <c>http://monitoring:8080</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Timeout in seconds for the registration HTTP call.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
