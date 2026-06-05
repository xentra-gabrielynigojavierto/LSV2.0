namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Classifies the mechanism/category used to monitor an entity.
/// Concrete check execution lives outside the domain — this enum only
/// describes what kind of probe is appropriate for the monitored target.
/// </summary>
public enum MonitoringType
{
    Http = 1,
    Database = 2,
    Storage = 3,
    Email = 4,
    Sms = 5,
    Other = 99,
}
