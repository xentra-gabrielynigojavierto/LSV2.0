namespace Comms.Application.Interfaces;

public record OutboundEmailPayload(
    Guid TenantId,
    string FromEmail,
    string FromDisplayName,
    string ToAddresses,
    string? CcAddresses,
    string? BccAddresses,
    string Subject,
    string? BodyText,
    string? BodyHtml,
    string InternetMessageId,
    string? InReplyToMessageId,
    string? ReferencesHeader,
    string IdempotencyKey,
    List<OutboundAttachmentDescriptor>? Attachments,
    string? ReplyToEmail = null,
    string? TemplateKey = null,
    Dictionary<string, string>? TemplateData = null);

public record OutboundAttachmentDescriptor(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long? FileSizeBytes);

public record NotificationsSendResult(
    bool Success,
    Guid? NotificationsRequestId,
    string? ProviderUsed,
    string? ProviderMessageId,
    string Status,
    string? ErrorMessage);

public interface INotificationsServiceClient
{
    Task<NotificationsSendResult> SendEmailAsync(OutboundEmailPayload payload, CancellationToken ct = default);
    Task<NotificationsSendResult> SendOperationalAlertAsync(DTOs.OperationalAlertPayload payload, CancellationToken ct = default);
}
