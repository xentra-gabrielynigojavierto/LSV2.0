using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskLinkedEntityRepository
{
    System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByEntityAsync(
        Guid   tenantId,
        string entityType,
        string entityId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskLinkedEntity?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    System.Threading.Tasks.Task AddAsync(
        TaskLinkedEntity entity, CancellationToken ct = default);

    /// <summary>
    /// TASK-B05 (TASK-017) — returns true if a linked-entity row already exists
    /// for the given (taskId, entityType, entityId) triple. Used for duplicate
    /// prevention before insert.
    /// </summary>
    System.Threading.Tasks.Task<bool> ExistsAsync(
        Guid   taskId,
        string entityType,
        string entityId,
        CancellationToken ct = default);

    void Remove(TaskLinkedEntity entity);
}
