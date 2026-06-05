using Monitoring.Domain.Monitoring;

namespace Monitoring.Api.Contracts;

/// <summary>
/// Request body for creating a monitored entity.
/// <para>
/// Required: <see cref="Name"/>, <see cref="EntityType"/>,
/// <see cref="MonitoringType"/>, <see cref="Target"/>.
/// </para>
/// <para>
/// Optional: <see cref="IsEnabled"/> (defaults to <c>true</c>),
/// <see cref="Scope"/> (defaults to <see cref="MonitoredEntityDefaults.Scope"/>),
/// <see cref="ImpactLevel"/> (defaults to <see cref="MonitoredEntityDefaults.Impact"/>).
/// </para>
/// </summary>
public sealed class CreateMonitoredEntityRequest
{
    public string? Name { get; set; }
    public EntityType? EntityType { get; set; }
    public MonitoringType? MonitoringType { get; set; }
    public string? Target { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Scope { get; set; }
    public ImpactLevel? ImpactLevel { get; set; }
}
