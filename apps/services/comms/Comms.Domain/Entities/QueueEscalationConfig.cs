namespace Comms.Domain.Entities;

public sealed class QueueEscalationConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid QueueId { get; private set; }
    public Guid? FallbackUserId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    private QueueEscalationConfig() { }

    public static QueueEscalationConfig Create(
        Guid tenantId, Guid queueId, Guid? fallbackUserId, Guid userId)
    {
        var now = DateTime.UtcNow;
        return new QueueEscalationConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            QueueId = queueId,
            FallbackUserId = fallbackUserId,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedByUserId = userId,
        };
    }

    public void Update(Guid? fallbackUserId, bool isActive, Guid userId)
    {
        FallbackUserId = fallbackUserId;
        IsActive = isActive;
        UpdatedByUserId = userId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
