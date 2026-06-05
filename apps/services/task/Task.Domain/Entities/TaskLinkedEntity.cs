namespace Task.Domain.Entities;

/// <summary>
/// A generic linkage record attaching an external entity reference to a <see cref="PlatformTask"/>.
/// Supports multiple related references per task (source context, related entities, workflow references, etc.)
/// without polluting the core task table.
/// MUST remain product-agnostic — no Liens-specific columns.
/// </summary>
public class TaskLinkedEntity
{
    public Guid    Id                { get; private set; }
    public Guid    TaskId            { get; private set; }
    public Guid    TenantId          { get; private set; }
    public string? SourceProductCode { get; private set; }
    public string  EntityType        { get; private set; } = string.Empty;
    public string  EntityId          { get; private set; } = string.Empty;
    public string  RelationshipType  { get; private set; } = LinkedEntityRelationship.Related;
    public DateTime CreatedAtUtc     { get; private set; }

    private TaskLinkedEntity() { }

    public static TaskLinkedEntity Create(
        Guid    taskId,
        Guid    tenantId,
        string  entityType,
        string  entityId,
        string  relationshipType  = LinkedEntityRelationship.Related,
        string? sourceProductCode = null)
    {
        if (taskId   == Guid.Empty) throw new ArgumentException("TaskId is required.",   nameof(taskId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        if (!LinkedEntityRelationship.All.Contains(relationshipType))
            throw new ArgumentException($"Invalid relationship type: '{relationshipType}'.", nameof(relationshipType));

        return new TaskLinkedEntity
        {
            Id               = Guid.NewGuid(),
            TaskId           = taskId,
            TenantId         = tenantId,
            SourceProductCode = sourceProductCode?.Trim().ToUpperInvariant(),
            EntityType       = entityType.Trim(),
            EntityId         = entityId.Trim(),
            RelationshipType = relationshipType,
            CreatedAtUtc     = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// Canonical relationship-type constants for <see cref="TaskLinkedEntity"/>.
/// </summary>
public static class LinkedEntityRelationship
{
    public const string Source   = "SOURCE";
    public const string Related  = "RELATED";
    public const string Workflow = "WORKFLOW";
    public const string Parent   = "PARENT";
    public const string Custom   = "CUSTOM";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Source, Related, Workflow, Parent, Custom };
}
