namespace Comms.Application.DTOs;

public record ConversationThreadResponse(
    Guid Id,
    Guid TenantId,
    Guid OrgId,
    string ProductKey,
    string ContextType,
    string ContextId,
    string Subject,
    string Status,
    string VisibilityType,
    DateTime LastActivityAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? CreatedByUserId,
    bool IsUnread,
    DateTime? LastReadAtUtc,
    Guid? LastReadMessageId,
    List<MessageResponse> Messages,
    List<ParticipantResponse> Participants);
