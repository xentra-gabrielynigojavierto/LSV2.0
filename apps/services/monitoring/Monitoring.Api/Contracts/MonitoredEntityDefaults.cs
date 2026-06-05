using Monitoring.Domain.Monitoring;

namespace Monitoring.Api.Contracts;

/// <summary>
/// Centralizes API-layer defaults applied when an optional classification
/// field is omitted from a create request. The defaults intentionally live
/// in the API layer (not the domain) because the domain treats every value
/// as caller-supplied; only the API has the notion of "the caller did not
/// say anything, pick something sensible."
/// </summary>
public static class MonitoredEntityDefaults
{
    /// <summary>
    /// Default <c>Scope</c> for newly created monitored entities when the
    /// caller does not provide one. The value is a generic platform-level
    /// bucket so the registry remains usable before any product-specific
    /// scope taxonomy is introduced.
    /// </summary>
    public const string Scope = "platform";

    /// <summary>
    /// Default <c>ImpactLevel</c> for newly created monitored entities when
    /// the caller does not provide one. <c>Optional</c> is the safest
    /// default — it never inflates the perceived blast radius of a check.
    /// </summary>
    public const ImpactLevel Impact = ImpactLevel.Optional;
}
