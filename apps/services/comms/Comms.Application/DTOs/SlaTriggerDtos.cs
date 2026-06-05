namespace Comms.Application.DTOs;

public record SlaTriggerEvaluationResponse(
    int EvaluatedConversationCount,
    int WarningsTriggered,
    int BreachesTriggered,
    int SkippedCount,
    DateTime EvaluatedAtUtc);

public record ConversationSlaTriggerStateResponse(
    Guid Id,
    Guid TenantId,
    Guid ConversationId,
    DateTime? FirstResponseWarningSentAtUtc,
    DateTime? FirstResponseBreachSentAtUtc,
    DateTime? ResolutionWarningSentAtUtc,
    DateTime? ResolutionBreachSentAtUtc,
    DateTime? LastEvaluatedAtUtc,
    Guid? LastEscalatedToUserId,
    Guid? LastEscalatedQueueId,
    int? WarningThresholdSnapshotMinutes,
    int? EvaluationVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record CreateQueueEscalationConfigRequest(
    Guid? FallbackUserId);

public record UpdateQueueEscalationConfigRequest(
    Guid? FallbackUserId,
    bool IsActive);

public record QueueEscalationConfigResponse(
    Guid Id,
    Guid TenantId,
    Guid QueueId,
    Guid? FallbackUserId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record OperationalAlertPayload(
    Guid TenantId,
    Guid ConversationId,
    Guid? QueueId,
    string Priority,
    string TriggerType,
    Guid TargetUserId,
    DateTime DueAtUtc,
    string? ConversationSubject,
    string IdempotencyKey);

public record EscalationTarget(
    Guid UserId,
    Guid? QueueId,
    string Source);
