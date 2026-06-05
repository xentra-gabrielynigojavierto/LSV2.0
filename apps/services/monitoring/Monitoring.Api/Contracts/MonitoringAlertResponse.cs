namespace Monitoring.Api.Contracts;

/// <summary>
/// Wire response for a single monitoring alert.
/// Severity values map AlertType to Control Center vocabulary:
/// "Info" | "Warning" | "Critical".
/// </summary>
public sealed record MonitoringAlertResponse(
    Guid AlertId,
    Guid EntityId,
    string Name,
    string Severity,
    string Message,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);
