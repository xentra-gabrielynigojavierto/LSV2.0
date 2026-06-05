namespace Comms.Domain.Entities;

public class MessageMention
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid MentionedUserId { get; private set; }
    public Guid MentionedByUserId { get; private set; }
    public bool IsMentionedUserParticipant { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private MessageMention() { }

    public static MessageMention Create(
        Guid tenantId,
        Guid conversationId,
        Guid messageId,
        Guid mentionedUserId,
        Guid mentionedByUserId,
        bool isMentionedUserParticipant)
    {
        return new MessageMention
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            MessageId = messageId,
            MentionedUserId = mentionedUserId,
            MentionedByUserId = mentionedByUserId,
            IsMentionedUserParticipant = isMentionedUserParticipant,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
