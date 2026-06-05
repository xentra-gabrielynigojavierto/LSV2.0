namespace Comms.Application.DTOs;

public record ConversationResponse(
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
    bool? IsUnread = null,
    DateTime? LastReadAtUtc = null);
