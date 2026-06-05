namespace Comms.Application.DTOs;

public record UpdateConversationPriorityRequest(string Priority);

public record ConversationSlaStateResponse(
    Guid Id,
    Guid TenantId,
    Guid ConversationId,
    string Priority,
    DateTime? FirstResponseDueAtUtc,
    DateTime? ResolutionDueAtUtc,
    DateTime? FirstResponseAtUtc,
    DateTime? ResolvedAtUtc,
    bool BreachedFirstResponse,
    bool BreachedResolution,
    string WaitingOn,
    DateTime? LastEvaluatedAtUtc,
    DateTime SlaStartedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record ConversationOperationalSummaryResponse(
    Guid ConversationId,
    string ConversationStatus,
    string Subject,
    ConversationQueueResponse? Queue,
    ConversationAssignmentResponse? Assignment,
    ConversationSlaStateResponse? SlaState,
    DateTime LastActivityAtUtc,
    DateTime CreatedAtUtc);

public record OperationalListQuery(
    Guid? QueueId = null,
    Guid? AssignedUserId = null,
    string? AssignmentStatus = null,
    string? Priority = null,
    bool? BreachedFirstResponse = null,
    bool? BreachedResolution = null,
    string? ConversationStatus = null);
