namespace Monitoring.Api.Contracts;

/// <summary>
/// Wire response for a single monitored entity's current status.
/// Status values are normalized to the Control Center vocabulary:
/// "Healthy" | "Degraded" | "Down".
/// </summary>
public sealed record MonitoringStatusResponse(
    Guid EntityId,
    string Name,
    string Scope,
    string Status,
    DateTime? LastCheckedAtUtc,
    long? LatencyMs);
