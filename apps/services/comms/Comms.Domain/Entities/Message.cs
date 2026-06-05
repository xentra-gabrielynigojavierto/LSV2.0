using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class Message : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }

    public string Channel { get; private set; } = Enums.Channel.InApp;
    public string Direction { get; private set; } = Enums.Direction.Internal;
    public string Body { get; private set; } = string.Empty;
    public string? BodyPlainText { get; private set; }
    public string VisibilityType { get; private set; } = Enums.VisibilityType.InternalOnly;

    public DateTime SentAtUtc { get; private set; }

    public Guid? SenderUserId { get; private set; }
    public string SenderParticipantType { get; private set; } = ParticipantType.InternalUser;
    public string? ExternalSenderName { get; private set; }
    public string? ExternalSenderEmail { get; private set; }

    public string Status { get; private set; } = MessageStatus.Created;

    private Message() { }

    public static Message Create(
        Guid conversationId,
        Guid tenantId,
        Guid orgId,
        string channel,
        string direction,
        string body,
        string visibilityType,
        Guid createdByUserId,
        Guid? senderUserId = null,
        string senderParticipantType = "InternalUser",
        string? externalSenderName = null,
        string? externalSenderEmail = null)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (!Enums.Channel.All.Contains(channel))
            throw new ArgumentException($"Invalid channel: '{channel}'.");

        if (!Enums.Direction.All.Contains(direction))
            throw new ArgumentException($"Invalid direction: '{direction}'.");

        if (!Enums.VisibilityType.All.Contains(visibilityType))
            throw new ArgumentException($"Invalid visibility type: '{visibilityType}'.");

        if (!ParticipantType.All.Contains(senderParticipantType))
            throw new ArgumentException($"Invalid sender participant type: '{senderParticipantType}'.");

        if (channel == Enums.Channel.SystemNote && visibilityType == Enums.VisibilityType.SharedExternal)
            throw new InvalidOperationException("System notes cannot have SharedExternal visibility.");

        var now = DateTime.UtcNow;
        return new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            TenantId = tenantId,
            OrgId = orgId,
            Channel = channel,
            Direction = direction,
            Body = body.Trim(),
            BodyPlainText = body.Trim(),
            VisibilityType = visibilityType,
            SentAtUtc = now,
            SenderUserId = senderUserId,
            SenderParticipantType = senderParticipantType,
            ExternalSenderName = externalSenderName?.Trim(),
            ExternalSenderEmail = externalSenderEmail?.Trim(),
            Status = MessageStatus.Posted,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
