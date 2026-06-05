namespace Comms.Application.DTOs;

public record ParticipantResponse(
    Guid Id,
    Guid ConversationId,
    string ParticipantType,
    Guid? UserId,
    string? ExternalName,
    string? ExternalEmail,
    string Role,
    bool CanReply,
    bool IsActive,
    DateTime JoinedAtUtc,
    DateTime CreatedAtUtc);
