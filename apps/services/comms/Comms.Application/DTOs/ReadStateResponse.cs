namespace Comms.Application.DTOs;

public record ReadStateResponse(
    Guid ConversationId,
    Guid UserId,
    bool IsUnread,
    Guid? LastReadMessageId,
    DateTime? LastReadAtUtc);
