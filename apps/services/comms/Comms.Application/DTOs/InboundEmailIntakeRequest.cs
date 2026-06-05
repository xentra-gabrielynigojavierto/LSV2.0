namespace Comms.Application.DTOs;

public record EmailAttachmentDescriptor(
    Guid? DocumentId,
    string FileName,
    string ContentType,
    long? FileSizeBytes);

public record InboundEmailIntakeRequest(
    string Provider,
    string InternetMessageId,
    string? ProviderMessageId,
    string? ProviderThreadId,
    string? InReplyToMessageId,
    string? ReferencesHeader,
    string FromEmail,
    string? FromDisplayName,
    string ToAddresses,
    string? CcAddresses,
    string Subject,
    string? TextBody,
    string? HtmlBody,
    DateTime ReceivedAtUtc,
    Guid TenantId,
    Guid OrgId,
    List<EmailAttachmentDescriptor>? Attachments = null);
