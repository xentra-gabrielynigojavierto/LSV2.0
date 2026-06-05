using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskLinkedEntityRepository : ITaskLinkedEntityRepository
{
    private readonly TasksDbContext _db;
    public TaskLinkedEntityRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
        => await _db.LinkedEntities
            .Where(e => e.TenantId == tenantId && e.TaskId == taskId)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntity>> GetByEntityAsync(
        Guid   tenantId,
        string entityType,
        string entityId,
        CancellationToken ct = default)
        => await _db.LinkedEntities
            .Where(e => e.TenantId   == tenantId
                     && e.EntityType == entityType
                     && e.EntityId   == entityId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<TaskLinkedEntity?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.LinkedEntities
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

    public async System.Threading.Tasks.Task<bool> ExistsAsync(
        Guid   taskId,
        string entityType,
        string entityId,
        CancellationToken ct = default)
        => await _db.LinkedEntities.AnyAsync(
            e => e.TaskId     == taskId
              && e.EntityType == entityType
              && e.EntityId   == entityId,
            ct);

    public async System.Threading.Tasks.Task AddAsync(
        TaskLinkedEntity entity, CancellationToken ct = default)
    {
        // TASK-B05 (TASK-017) — dedup guard: skip silently if the link already exists.
        // The DB unique constraint (UX_LinkedEntities_TaskId_EntityType_EntityId) is the
        // authoritative safety net; this check avoids throwing on the happy-path duplicate.
        var exists = await ExistsAsync(
            entity.TaskId, entity.EntityType, entity.EntityId, ct);
        if (!exists)
            await _db.LinkedEntities.AddAsync(entity, ct);
    }

    public void Remove(TaskLinkedEntity entity)
        => _db.LinkedEntities.Remove(entity);
}
