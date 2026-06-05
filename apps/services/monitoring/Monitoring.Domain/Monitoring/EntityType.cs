namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Classifies whether a monitored entity is owned by the platform or operated
/// by a third party.
/// </summary>
public enum EntityType
{
    /// <summary>An internal LegalSynq platform service.</summary>
    InternalService = 1,

    /// <summary>A third-party dependency the platform relies on.</summary>
    ExternalDependency = 2,
}
