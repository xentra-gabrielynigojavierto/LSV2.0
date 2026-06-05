namespace Liens.Application.Interfaces;

public sealed record TaskGenerationContext(
    Guid    TenantId,
    string  EventType,
    string  EntityType,
    Guid    EntityId,
    Guid?   CaseId,
    Guid?   LienId,
    Guid?   WorkflowStageId,
    Guid?   ActorUserId,
    string? ActorName = null);

public sealed record TaskGenerationResult(int TasksGenerated, int TasksSkipped);

public interface ILienTaskGenerationEngine
{
    Task<TaskGenerationResult> TriggerAsync(TaskGenerationContext context, CancellationToken ct = default);
}
