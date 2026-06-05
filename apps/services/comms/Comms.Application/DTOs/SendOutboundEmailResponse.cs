namespace Comms.Application.DTOs;

public record SendOutboundEmailResponse(
    Guid ConversationId,
    Guid MessageId,
    Guid EmailMessageReferenceId,
    string DeliveryStatus,
    Guid? NotificationsRequestId,
    string GeneratedInternetMessageId,
    Guid? MatchedReplyReferenceId,
    int AttachmentCount,
    Guid? SenderConfigId = null,
    string? SenderEmail = null,
    string? TemplateKey = null,
    Guid? TemplateConfigId = null,
    string? RenderedSubject = null,
    string? CompositionMode = null);
