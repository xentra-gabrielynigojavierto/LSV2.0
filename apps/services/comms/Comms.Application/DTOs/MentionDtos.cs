namespace Comms.Application.DTOs;

public record MentionResponse(
    Guid Id,
    Guid MentionedUserId,
    Guid MentionedByUserId,
    bool IsMentionedUserParticipant,
    DateTime CreatedAtUtc);

public record MentionNotificationPayload(
    Guid TenantId,
    Guid ConversationId,
    Guid MessageId,
    Guid MentionedUserId,
    Guid MentionedByUserId,
    string SummarySnippet,
    string IdempotencyKey);
