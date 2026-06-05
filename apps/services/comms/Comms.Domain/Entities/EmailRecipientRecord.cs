using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class EmailRecipientRecord : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid EmailMessageReferenceId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string RecipientType { get; private set; } = Enums.RecipientType.To;
    public string RecipientVisibility { get; private set; } = Enums.RecipientVisibility.Visible;
    public bool IsResolvedToParticipant { get; private set; }
    public string? RecipientSource { get; private set; }

    private EmailRecipientRecord() { }

    public static EmailRecipientRecord Create(
        Guid tenantId,
        Guid conversationId,
        Guid emailMessageReferenceId,
        string email,
        string recipientType,
        string? displayName,
        Guid? createdByUserId,
        string? recipientSource = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (emailMessageReferenceId == Guid.Empty) throw new ArgumentException("EmailMessageReferenceId is required.", nameof(emailMessageReferenceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (!Enums.RecipientType.IsValid(recipientType))
            throw new ArgumentException($"Invalid recipient type: {recipientType}", nameof(recipientType));

        var now = DateTime.UtcNow;
        return new EmailRecipientRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            EmailMessageReferenceId = emailMessageReferenceId,
            NormalizedEmail = EmailMessageReference.NormalizeEmail(email),
            DisplayName = displayName?.Trim(),
            RecipientType = recipientType,
            RecipientVisibility = Enums.RecipientVisibility.FromRecipientType(recipientType),
            IsResolvedToParticipant = false,
            RecipientSource = recipientSource,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void LinkParticipant(Guid participantId, Guid? updatedByUserId)
    {
        ParticipantId = participantId;
        IsResolvedToParticipant = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
