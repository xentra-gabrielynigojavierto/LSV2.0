namespace Comms.Application.DTOs;

public record DeliveryStatusUpdateRequest(
    string Provider,
    string? ProviderMessageId,
    string? InternetMessageId,
    string Status,
    DateTime StatusAtUtc,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int? RetryCount = null,
    string? ProviderPayloadReference = null,
    string? NotificationsRequestId = null);
