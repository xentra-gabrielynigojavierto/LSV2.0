using BuildingBlocks.Domain;

namespace Comms.Domain.Entities;

public class EmailMessageReference : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }

    public string? ProviderMessageId { get; private set; }
    public string InternetMessageId { get; private set; } = string.Empty;
    public string? InReplyToMessageId { get; private set; }
    public string? ReferencesHeader { get; private set; }
    public string? ProviderThreadId { get; private set; }

    public string EmailDirection { get; private set; } = "Inbound";

    public string FromEmail { get; private set; } = string.Empty;
    public string? FromDisplayName { get; private set; }
    public string ToAddresses { get; private set; } = string.Empty;
    public string? CcAddresses { get; private set; }
    public string Subject { get; private set; } = string.Empty;

    public Guid? SenderConfigId { get; private set; }
    public string? SenderConfigEmail { get; private set; }
    public Guid? TemplateConfigId { get; private set; }
    public string? TemplateKey { get; private set; }
    public string? CompositionMode { get; private set; }

    public DateTime? ReceivedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    private EmailMessageReference() { }

    public static EmailMessageReference Create(
        Guid tenantId,
        Guid conversationId,
        Guid? messageId,
        string internetMessageId,
        string emailDirection,
        string fromEmail,
        string toAddresses,
        string subject,
        Guid? createdByUserId,
        string? providerMessageId = null,
        string? inReplyToMessageId = null,
        string? referencesHeader = null,
        string? providerThreadId = null,
        string? fromDisplayName = null,
        string? ccAddresses = null,
        DateTime? receivedAtUtc = null,
        DateTime? sentAtUtc = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(internetMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddresses);

        var now = DateTime.UtcNow;
        return new EmailMessageReference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            MessageId = messageId,
            ProviderMessageId = providerMessageId?.Trim(),
            InternetMessageId = internetMessageId.Trim(),
            InReplyToMessageId = inReplyToMessageId?.Trim(),
            ReferencesHeader = referencesHeader?.Trim(),
            ProviderThreadId = providerThreadId?.Trim(),
            EmailDirection = emailDirection,
            FromEmail = NormalizeEmail(fromEmail),
            FromDisplayName = fromDisplayName?.Trim(),
            ToAddresses = toAddresses.Trim(),
            CcAddresses = ccAddresses?.Trim(),
            Subject = subject.Trim(),
            ReceivedAtUtc = receivedAtUtc,
            SentAtUtc = sentAtUtc,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void SetCompositionMetadata(
        Guid? senderConfigId, string? senderConfigEmail,
        Guid? templateConfigId, string? templateKey,
        string compositionMode, Guid? updatedByUserId)
    {
        SenderConfigId = senderConfigId;
        SenderConfigEmail = senderConfigEmail?.Trim().ToLowerInvariant();
        TemplateConfigId = templateConfigId;
        TemplateKey = templateKey?.Trim().ToLowerInvariant();
        CompositionMode = compositionMode;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetProviderMessageId(string providerMessageId, Guid? updatedByUserId)
    {
        ProviderMessageId = providerMessageId?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetSentAtUtc(DateTime sentAt, Guid? updatedByUserId)
    {
        SentAtUtc = sentAt;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public static string GenerateInternetMessageId(Guid conversationId, string domain = "comms.legalsynq.com")
    {
        return $"<{conversationId:N}.{Guid.NewGuid():N}@{domain}>";
    }
}
