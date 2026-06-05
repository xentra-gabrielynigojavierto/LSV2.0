namespace Comms.Application.DTOs;

public record CreateConversationQueueRequest(
    string Name,
    string Code,
    string? Description,
    bool IsDefault);

public record UpdateConversationQueueRequest(
    string Name,
    string? Description,
    bool IsActive);

public record ConversationQueueResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Code,
    string? Description,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? CreatedByUserId);
