namespace Comms.Application.DTOs;

public record AddParticipantRequest(
    string ParticipantType,
    string Role,
    bool CanReply = true,
    Guid? UserId = null,
    string? ExternalName = null,
    string? ExternalEmail = null);
