using Monitoring.Domain.Monitoring;

namespace Monitoring.Api.Contracts;

/// <summary>
/// PATCH request body for partially updating a monitored entity.
/// Patch semantics: any field omitted (null) is left unchanged; any field
/// present is applied through the corresponding domain method, which enforces
/// invariants. To target only one half of the (EntityType, MonitoringType)
/// pair, omit the other and the existing value is preserved.
/// </summary>
public sealed class UpdateMonitoredEntityRequest
{
    public string? Name { get; set; }
    public EntityType? EntityType { get; set; }
    public MonitoringType? MonitoringType { get; set; }
    public string? Target { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Scope { get; set; }
    public ImpactLevel? ImpactLevel { get; set; }
}
