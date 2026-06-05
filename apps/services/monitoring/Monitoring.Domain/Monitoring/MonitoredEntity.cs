using Monitoring.Domain.Common;

namespace Monitoring.Domain.Monitoring;

/// <summary>
/// A monitored entity — either an internal LegalSynq service or an external
/// dependency the platform relies on. This is the first persisted domain
/// model; later features (registry APIs, check execution, status evaluation,
/// alerting) will reference it without modifying its schema.
/// </summary>
public class MonitoredEntity : IAuditableEntity
{
    public const int NameMaxLength = 200;
    public const int TargetMaxLength = 1000;
    public const int ScopeMaxLength = 100;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public EntityType EntityType { get; private set; }
    public MonitoringType MonitoringType { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Generic, free-form product/platform grouping this entity belongs to
    /// (for example <c>"platform"</c>, <c>"billing"</c>, <c>"legal-ai"</c>).
    /// Stored as supplied (trimmed); no canonical taxonomy is enforced here
    /// — that is intentionally deferred until a scope catalog feature lands.
    /// </summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>
    /// How impactful this entity's failure or degradation is. Drives later
    /// alerting/summarization (out of scope for the registry feature).
    /// </summary>
    public ImpactLevel ImpactLevel { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>EF Core constructor. Do not use from application code.</summary>
    private MonitoredEntity() { }

    public MonitoredEntity(
        Guid id,
        string name,
        EntityType entityType,
        MonitoringType monitoringType,
        string target,
        string scope,
        ImpactLevel impactLevel,
        bool isEnabled = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must be a non-empty Guid.", nameof(id));
        }

        Id = id;
        Rename(name);
        Retarget(target);
        ChangeClassification(entityType, monitoringType);
        Rescope(scope);
        ChangeImpact(impactLevel);
        IsEnabled = isEnabled;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required and cannot be blank.", nameof(name));
        }

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException(
                $"Name must be at most {NameMaxLength} characters.", nameof(name));
        }

        Name = name.Trim();
    }

    public void Retarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target is required and cannot be blank.", nameof(target));
        }

        if (target.Length > TargetMaxLength)
        {
            throw new ArgumentException(
                $"Target must be at most {TargetMaxLength} characters.", nameof(target));
        }

        Target = target.Trim();
    }

    public void ChangeClassification(EntityType entityType, MonitoringType monitoringType)
    {
        if (!Enum.IsDefined(typeof(EntityType), entityType))
        {
            throw new ArgumentException("Unknown EntityType value.", nameof(entityType));
        }

        if (!Enum.IsDefined(typeof(MonitoringType), monitoringType))
        {
            throw new ArgumentException("Unknown MonitoringType value.", nameof(monitoringType));
        }

        EntityType = entityType;
        MonitoringType = monitoringType;
    }

    /// <summary>
    /// Updates the entity's product/platform <see cref="Scope"/>. Required,
    /// non-blank, trimmed, and bounded by <see cref="ScopeMaxLength"/>.
    /// </summary>
    public void Rescope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope is required and cannot be blank.", nameof(scope));
        }

        var trimmed = scope.Trim();
        if (trimmed.Length > ScopeMaxLength)
        {
            throw new ArgumentException(
                $"Scope must be at most {ScopeMaxLength} characters.", nameof(scope));
        }

        Scope = trimmed;
    }

    /// <summary>
    /// Updates the entity's <see cref="ImpactLevel"/>. Validated via
    /// <see cref="Enum.IsDefined(Type, object)"/> consistent with
    /// <see cref="ChangeClassification"/>.
    /// </summary>
    public void ChangeImpact(ImpactLevel impactLevel)
    {
        if (!Enum.IsDefined(typeof(ImpactLevel), impactLevel))
        {
            throw new ArgumentException("Unknown ImpactLevel value.", nameof(impactLevel));
        }

        ImpactLevel = impactLevel;
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    void IAuditableEntity.SetCreatedAt(DateTime utcNow) => CreatedAtUtc = utcNow;

    void IAuditableEntity.SetUpdatedAt(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
