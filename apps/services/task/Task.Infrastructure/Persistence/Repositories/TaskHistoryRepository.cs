using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskHistoryRepository : ITaskHistoryRepository
{
    private readonly TasksDbContext _db;
    public TaskHistoryRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskHistory>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
        => await _db.History
            .Where(h => h.TenantId == tenantId && h.TaskId == taskId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(TaskHistory entry, CancellationToken ct = default)
        => await _db.History.AddAsync(entry, ct);
}
