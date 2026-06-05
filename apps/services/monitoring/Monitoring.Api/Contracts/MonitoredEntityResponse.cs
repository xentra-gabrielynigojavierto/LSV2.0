using Monitoring.Domain.Monitoring;

namespace Monitoring.Api.Contracts;

/// <summary>
/// API response shape for a monitored entity. Stable contract that does not
/// leak the EF-mapped domain entity directly.
/// </summary>
public sealed class MonitoredEntityResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public EntityType EntityType { get; init; }
    public MonitoringType MonitoringType { get; init; }
    public bool IsEnabled { get; init; }
    public string Scope { get; init; } = string.Empty;
    public ImpactLevel ImpactLevel { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    public static MonitoredEntityResponse From(MonitoredEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        EntityType = entity.EntityType,
        MonitoringType = entity.MonitoringType,
        IsEnabled = entity.IsEnabled,
        Scope = entity.Scope,
        ImpactLevel = entity.ImpactLevel,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };
}
