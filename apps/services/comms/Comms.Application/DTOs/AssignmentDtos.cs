namespace Comms.Application.DTOs;

public record AssignConversationRequest(
    Guid? QueueId,
    Guid? AssignedUserId,
    string? Priority);

public record ReassignConversationRequest(
    Guid? QueueId,
    Guid? AssignedUserId);

public record UnassignConversationRequest();

public record AcceptConversationAssignmentRequest();

public record ConversationAssignmentResponse(
    Guid Id,
    Guid TenantId,
    Guid ConversationId,
    Guid? QueueId,
    string? QueueName,
    Guid? AssignedUserId,
    Guid? AssignedByUserId,
    string AssignmentStatus,
    DateTime AssignedAtUtc,
    DateTime LastAssignedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? UnassignedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
