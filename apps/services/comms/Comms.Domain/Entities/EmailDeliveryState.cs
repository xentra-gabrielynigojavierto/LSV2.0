using BuildingBlocks.Domain;
using Comms.Domain.Enums;

namespace Comms.Domain.Entities;

public class EmailDeliveryState : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid EmailMessageReferenceId { get; private set; }

    public string DeliveryStatus { get; private set; } = Enums.DeliveryStatus.Pending;
    public string? ProviderName { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? NotificationsRequestId { get; private set; }
    public DateTime? LastStatusAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    private EmailDeliveryState() { }

    public static EmailDeliveryState Create(
        Guid tenantId,
        Guid conversationId,
        Guid messageId,
        Guid emailMessageReferenceId,
        string? notificationsRequestId,
        string? providerName,
        string? providerMessageId,
        string initialStatus,
        Guid? createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        if (messageId == Guid.Empty) throw new ArgumentException("MessageId is required.", nameof(messageId));
        if (emailMessageReferenceId == Guid.Empty) throw new ArgumentException("EmailMessageReferenceId is required.", nameof(emailMessageReferenceId));

        var now = DateTime.UtcNow;
        return new EmailDeliveryState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            MessageId = messageId,
            EmailMessageReferenceId = emailMessageReferenceId,
            DeliveryStatus = initialStatus,
            ProviderName = providerName,
            ProviderMessageId = providerMessageId,
            NotificationsRequestId = notificationsRequestId,
            LastStatusAtUtc = now,
            RetryCount = 0,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public bool UpdateStatus(
        string newStatus,
        DateTime statusAtUtc,
        string? errorCode,
        string? errorMessage,
        int? retryCount,
        string? providerMessageId,
        Guid? updatedByUserId)
    {
        if (Enums.DeliveryStatus.IsTerminal(DeliveryStatus))
            return false;

        if (LastStatusAtUtc.HasValue && statusAtUtc < LastStatusAtUtc.Value)
            return false;

        DeliveryStatus = newStatus;
        LastStatusAtUtc = statusAtUtc;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;

        if (retryCount.HasValue)
            RetryCount = retryCount.Value;

        if (!string.IsNullOrWhiteSpace(providerMessageId))
            ProviderMessageId = providerMessageId;

        return true;
    }
}
