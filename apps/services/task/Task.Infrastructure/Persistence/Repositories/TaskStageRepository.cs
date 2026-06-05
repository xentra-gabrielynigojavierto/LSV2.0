using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskStageRepository : ITaskStageRepository
{
    private readonly TasksDbContext _db;
    public TaskStageRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<TaskStageConfig?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.StageConfigs
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    public async System.Threading.Tasks.Task<TaskStageConfig?> GetByIdAnyTenantAsync(
        Guid id, CancellationToken ct = default)
        => await _db.StageConfigs
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskStageConfig>> GetByTenantAsync(
        Guid tenantId, string? sourceProductCode = null, bool activeOnly = true, CancellationToken ct = default)
    {
        var q = _db.StageConfigs.Where(s => s.TenantId == tenantId);

        if (sourceProductCode is not null)
            q = q.Where(s => s.SourceProductCode == sourceProductCode.ToUpperInvariant()
                           || s.SourceProductCode == null);
        if (activeOnly)
            q = q.Where(s => s.IsActive);

        return await q.OrderBy(s => s.DisplayOrder).ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task AddAsync(
        TaskStageConfig stage, CancellationToken ct = default)
        => await _db.StageConfigs.AddAsync(stage, ct);

    public System.Threading.Tasks.Task UpdateAsync(
        TaskStageConfig stage, CancellationToken ct = default)
    {
        _db.StageConfigs.Update(stage);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
