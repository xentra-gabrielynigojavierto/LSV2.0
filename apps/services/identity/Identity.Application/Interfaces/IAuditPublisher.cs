namespace Identity.Application.Interfaces;

public interface IAuditPublisher
{
    void Publish(
        string eventType,
        string action,
        string description,
        Guid? tenantId,
        Guid? actorUserId = null,
        string? entityType = null,
        string? entityId = null,
        string? before = null,
        string? after = null,
        string? metadata = null,
        string? correlationId = null);
}
