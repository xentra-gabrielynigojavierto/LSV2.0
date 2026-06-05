namespace Comms.Application.DTOs;

public record EmailDeliveryStateResponse(
    Guid Id,
    Guid ConversationId,
    Guid MessageId,
    Guid EmailMessageReferenceId,
    string DeliveryStatus,
    string? ProviderName,
    string? ProviderMessageId,
    string? NotificationsRequestId,
    DateTime? LastStatusAtUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    int RetryCount,
    DateTime CreatedAtUtc);
