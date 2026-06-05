using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class ConversationParticipant : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }

    public string ParticipantType { get; private set; } = Enums.ParticipantType.InternalUser;
    public Guid? UserId { get; private set; }
    public string? ExternalName { get; private set; }
    public string? ExternalEmail { get; private set; }

    public string Role { get; private set; } = ParticipantRole.Participant;
    public bool CanReply { get; private set; } = true;
    public bool IsActive { get; private set; } = true;

    public DateTime JoinedAtUtc { get; private set; }

    private ConversationParticipant() { }

    public static ConversationParticipant Create(
        Guid conversationId,
        Guid tenantId,
        Guid orgId,
        string participantType,
        string role,
        bool canReply,
        Guid createdByUserId,
        Guid? userId = null,
        string? externalName = null,
        string? externalEmail = null)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (!Enums.ParticipantType.All.Contains(participantType))
            throw new ArgumentException($"Invalid participant type: '{participantType}'.");

        if (!ParticipantRole.All.Contains(role))
            throw new ArgumentException($"Invalid role: '{role}'.");

        if (participantType == Enums.ParticipantType.InternalUser && userId == null)
            throw new ArgumentException("UserId is required for internal user participants.");

        if (participantType == Enums.ParticipantType.ExternalContact &&
            string.IsNullOrWhiteSpace(externalName) && string.IsNullOrWhiteSpace(externalEmail))
            throw new ArgumentException("External contact must have a name or email.");

        var now = DateTime.UtcNow;
        return new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            TenantId = tenantId,
            OrgId = orgId,
            ParticipantType = participantType,
            UserId = userId,
            ExternalName = externalName?.Trim(),
            ExternalEmail = externalEmail?.Trim(),
            Role = role,
            CanReply = canReply,
            IsActive = true,
            JoinedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
