using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskRepository
{
    Task<LienTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<LienTask> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        string? search,
        string? status,
        string? priority,
        Guid? assignedUserId,
        Guid? caseId,
        Guid? lienId,
        Guid? workflowStageId,
        string? assignmentScope,
        Guid? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<List<LienTaskLienLink>> GetLienLinksForTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<List<LienTaskLienLink>> GetTaskLinksForLienAsync(Guid tenantId, Guid lienId, CancellationToken ct = default);
    Task AddAsync(LienTask entity, CancellationToken ct = default);
    Task UpdateAsync(LienTask entity, CancellationToken ct = default);
    Task AddLienLinksAsync(IEnumerable<LienTaskLienLink> links, CancellationToken ct = default);
    Task RemoveLienLinksAsync(Guid taskId, CancellationToken ct = default);

    // TASK-B05 (TASK-019) — HasOpenTaskForRuleAsync / HasOpenTaskForTemplateAsync removed;
    // duplicate-prevention is now handled server-side via ILiensTaskServiceClient
    // (Task service HTTP calls) so the local Liens DB is no longer queried for this purpose.
    Task AddGeneratedMetadataAsync(LienGeneratedTaskMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// TASK-B04 — cross-tenant paginated scan for the one-shot backfill operation.
    /// Returns all tasks (any tenant) ordered by CreatedAtUtc ascending.
    /// </summary>
    Task<List<LienTask>> GetAllPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// LS-LIENS-FLOW-009 — returns all tasks for <paramref name="tenantId"/> linked to
    /// <paramref name="workflowInstanceId"/>. Uses the
    /// <c>IX_Tasks_TenantId_WorkflowInstanceId</c> index; returns an empty list when none found.
    /// </summary>
    Task<List<LienTask>> GetByWorkflowInstanceIdAsync(
        Guid tenantId,
        Guid workflowInstanceId,
        CancellationToken ct = default);
}
