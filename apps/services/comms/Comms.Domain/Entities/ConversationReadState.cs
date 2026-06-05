using BuildingBlocks.Domain;

namespace Comms.Domain.Entities;

public class ConversationReadState : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? LastReadMessageId { get; private set; }
    public DateTime? LastReadAtUtc { get; private set; }

    private ConversationReadState() { }

    public static ConversationReadState Create(
        Guid tenantId,
        Guid conversationId,
        Guid userId,
        Guid? lastReadMessageId,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        var now = DateTime.UtcNow;
        return new ConversationReadState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            UserId = userId,
            LastReadMessageId = lastReadMessageId,
            LastReadAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void MarkRead(Guid messageId, Guid updatedByUserId)
    {
        LastReadMessageId = messageId;
        LastReadAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearReadState(Guid updatedByUserId)
    {
        LastReadMessageId = null;
        LastReadAtUtc = null;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
