namespace Comms.Application.DTOs;

public record CreateConversationRequest(
    string ProductKey,
    string ContextType,
    string ContextId,
    string Subject,
    string VisibilityType);
