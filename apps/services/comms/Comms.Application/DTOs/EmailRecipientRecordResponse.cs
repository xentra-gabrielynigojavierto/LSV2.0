namespace Comms.Application.DTOs;

public record EmailRecipientRecordResponse(
    Guid Id,
    string NormalizedEmail,
    string? DisplayName,
    string RecipientType,
    string RecipientVisibility,
    Guid? ParticipantId,
    bool IsResolvedToParticipant);
