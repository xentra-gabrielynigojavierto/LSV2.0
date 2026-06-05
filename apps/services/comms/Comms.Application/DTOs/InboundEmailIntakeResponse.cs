namespace Comms.Application.DTOs;

public record InboundEmailIntakeResponse(
    Guid ConversationId,
    bool CreatedNewConversation,
    bool CreatedNewParticipant,
    Guid LinkedMessageId,
    string MatchedBy,
    int AttachmentCountProcessed,
    Guid EmailReferenceId);
