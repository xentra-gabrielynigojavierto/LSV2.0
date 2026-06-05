namespace Comms.Application.DTOs;

public record MessageResponse(
    Guid Id,
    Guid ConversationId,
    string Channel,
    string Direction,
    string Body,
    string VisibilityType,
    DateTime SentAtUtc,
    Guid? SenderUserId,
    string SenderParticipantType,
    string? ExternalSenderName,
    string? ExternalSenderEmail,
    string Status,
    DateTime CreatedAtUtc,
    List<AttachmentResponse>? Attachments = null,
    List<Guid>? Mentions = null);
