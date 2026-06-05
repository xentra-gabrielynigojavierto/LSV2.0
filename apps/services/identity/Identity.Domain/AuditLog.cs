namespace Identity.Domain;

public class AuditLog
{
    public Guid Id { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string ActorType { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string actorName,
        string actorType,
        string action,
        string entityType,
        string entityId,
        string? metadataJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        return new AuditLog
        {
            Id           = Guid.NewGuid(),
            ActorName    = actorName.Trim(),
            ActorType    = actorType.Trim(),
            Action       = action.Trim(),
            EntityType   = entityType.Trim(),
            EntityId     = entityId.Trim(),
            MetadataJson = metadataJson,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
