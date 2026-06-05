using Microsoft.EntityFrameworkCore;
using Task.Application.Repositories;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskStageTransitionRepository : ITaskStageTransitionRepository
{
    private readonly TasksDbContext _db;
    public TaskStageTransitionRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<List<TaskStageTransition>> GetActiveByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default)
        => await _db.StageTransitions
            .Where(t => t.TenantId == tenantId
                     && t.SourceProductCode == productCode.ToUpperInvariant()
                     && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<TaskStageTransition?> GetByTenantProductStagesAsync(
        Guid tenantId, string productCode, Guid fromStageId, Guid toStageId, CancellationToken ct = default)
        => await _db.StageTransitions
            .FirstOrDefaultAsync(t => t.TenantId          == tenantId
                                   && t.SourceProductCode  == productCode.ToUpperInvariant()
                                   && t.FromStageId        == fromStageId
                                   && t.ToStageId          == toStageId, ct);

    public async System.Threading.Tasks.Task AddAsync(
        TaskStageTransition transition, CancellationToken ct = default)
        => await _db.StageTransitions.AddAsync(transition, ct);

    public System.Threading.Tasks.Task UpdateAsync(
        TaskStageTransition transition, CancellationToken ct = default)
    {
        _db.StageTransitions.Update(transition);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task AddRangeAsync(
        IEnumerable<TaskStageTransition> transitions, CancellationToken ct = default)
        => await _db.StageTransitions.AddRangeAsync(transitions, ct);

    public async System.Threading.Tasks.Task DeactivateAllAsync(
        Guid tenantId, string productCode, CancellationToken ct = default)
        => await _db.StageTransitions
            .Where(t => t.TenantId         == tenantId
                     && t.SourceProductCode == productCode.ToUpperInvariant()
                     && t.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsActive, false)
                .SetProperty(t => t.UpdatedAtUtc, DateTime.UtcNow), ct);
}
