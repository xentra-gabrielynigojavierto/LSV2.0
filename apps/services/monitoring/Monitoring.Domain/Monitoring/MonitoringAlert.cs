using Monitoring.Domain.Common;

namespace Monitoring.Domain.Monitoring;

/// <summary>
/// Durable, mutable alert record produced by the alert rule engine.
///
/// <para><b>Lifecycle (this feature)</b>:
/// <list type="bullet">
///   <item><b>Created</b> as <c>IsActive=true</c> when an entity
///     transitions into <see cref="EntityStatus.Down"/>.</item>
///   <item><b>Suppressed</b> while an active row already exists for the
///     same <c>(MonitoredEntityId, AlertType)</c> — handled by the
///     engine, no new row is written.</item>
///   <item><b>Resolved</b> by the engine when the entity transitions
///     from <see cref="EntityStatus.Down"/> back to
///     <see cref="EntityStatus.Up"/> or <see cref="EntityStatus.Unknown"/>:
///     <c>IsActive=false</c>, <see cref="ResolvedAtUtc"/> is set.</item>
/// </list>
/// </para>
///
/// <para><b>Snapshotting</b>: <see cref="EntityName"/>, <see cref="Scope"/>,
/// and <see cref="ImpactLevel"/> are copied from the producing
/// <c>MonitoredEntity</c> at fire time so historical alerts remain
/// readable even if the entity is later renamed, rescoped, or has its
/// impact reclassified.</para>
///
/// <para><b>Out of scope (deferred)</b>: acknowledgement, assignee,
/// escalation, incident grouping, notification delivery, alert history
/// event log. None of those are introduced here.</para>
///
/// <para><b>Audit</b>: implements <see cref="IAuditableEntity"/>;
/// <see cref="CreatedAtUtc"/> / <see cref="UpdatedAtUtc"/> are stamped
/// by the SaveChanges interceptor on <c>MonitoringDbContext</c>.</para>
/// </summary>
public class MonitoringAlert : IAuditableEntity
{
    public const int EntityNameMaxLength = 200;
    public const int ScopeMaxLength = 100;
    public const int MessageMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid MonitoredEntityId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public ImpactLevel ImpactLevel { get; private set; }

    public EntityStatus PreviousStatus { get; private set; }
    public EntityStatus CurrentStatus { get; private set; }
    public AlertType AlertType { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime TriggeredAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private MonitoringAlert() { }

    /// <summary>
    /// Creates a fresh active alert. Used by the rule engine the first
    /// time a transition fires for a given entity + alert type.
    /// </summary>
    public MonitoringAlert(
        Guid id,
        Guid monitoredEntityId,
        string entityName,
        string scope,
        ImpactLevel impactLevel,
        EntityStatus previousStatus,
        EntityStatus currentStatus,
        AlertType alertType,
        DateTime triggeredAtUtc,
        string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty Guid.", nameof(id));
        }

        if (monitoredEntityId == Guid.Empty)
        {
            throw new ArgumentException(
                "MonitoredEntityId must be a non-empty Guid.", nameof(monitoredEntityId));
        }

        Id = id;
        MonitoredEntityId = monitoredEntityId;
        EntityName = Truncate(entityName ?? string.Empty, EntityNameMaxLength);
        Scope = Truncate(scope ?? string.Empty, ScopeMaxLength);
        ImpactLevel = impactLevel;
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
        AlertType = alertType;
        IsActive = true;
        TriggeredAtUtc = triggeredAtUtc;
        ResolvedAtUtc = null;
        Message = Truncate(message ?? string.Empty, MessageMaxLength);
    }

    /// <summary>
    /// Marks this alert resolved. Idempotent: calling it twice does not
    /// shift <see cref="ResolvedAtUtc"/> or re-flip <see cref="IsActive"/>.
    /// </summary>
    public void Resolve(DateTime resolvedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ResolvedAtUtc = resolvedAtUtc;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    void IAuditableEntity.SetCreatedAt(DateTime utcNow) => CreatedAtUtc = utcNow;
    void IAuditableEntity.SetUpdatedAt(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
