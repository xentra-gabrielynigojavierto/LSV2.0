namespace Comms.Application.DTOs;

public record EmailReferenceResponse(
    Guid Id,
    Guid ConversationId,
    Guid? MessageId,
    string InternetMessageId,
    string? ProviderMessageId,
    string? ProviderThreadId,
    string? InReplyToMessageId,
    string EmailDirection,
    string FromEmail,
    string? FromDisplayName,
    string ToAddresses,
    string? CcAddresses,
    string Subject,
    DateTime? ReceivedAtUtc,
    DateTime? SentAtUtc,
    DateTime CreatedAtUtc);
