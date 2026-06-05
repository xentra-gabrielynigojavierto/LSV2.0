namespace Monitoring.Api.Contracts;

/// <summary>
/// Wire response for the full monitoring summary consumed by the Control Center.
/// Maps directly to the MonitoringSummary type in monitoring-source.ts.
/// </summary>
public sealed record MonitoringSummaryResponse(
    MonitoringSystemStatusResponse System,
    IReadOnlyList<MonitoringStatusResponse> Integrations,
    IReadOnlyList<MonitoringAlertResponse> Alerts);

/// <summary>Top-level system health status, derived as the worst status across all entities.</summary>
public sealed record MonitoringSystemStatusResponse(
    string Status,
    DateTime LastCheckedAtUtc);
