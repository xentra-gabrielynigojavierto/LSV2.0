namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Classification of a monitoring alert. Stable, explicit, persisted as
/// a string so future values can be added without renumbering.
///
/// <para>Currently only <see cref="StatusDown"/> is produced by the
/// rule engine. Additional categories (degraded, unknown, latency,
/// flapping, etc.) will be added when the corresponding rules land —
/// adding values does not require a schema change.</para>
/// </summary>
public enum AlertType
{
    /// <summary>Entity transitioned into <c>EntityStatus.Down</c>.</summary>
    StatusDown = 1,
}
