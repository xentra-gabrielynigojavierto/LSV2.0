using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskNoteRepository : ITaskNoteRepository
{
    private readonly TasksDbContext _db;
    public TaskNoteRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskNote>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
        => await _db.Notes
            .Where(n => n.TenantId == tenantId && n.TaskId == taskId && !n.IsDeleted)
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<TaskNote?> GetByIdAsync(
        Guid tenantId, Guid noteId, CancellationToken ct = default)
        => await _db.Notes
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == noteId, ct);

    public async System.Threading.Tasks.Task AddAsync(TaskNote note, CancellationToken ct = default)
        => await _db.Notes.AddAsync(note, ct);

    public System.Threading.Tasks.Task UpdateAsync(TaskNote note, CancellationToken ct = default)
    {
        _db.Notes.Update(note);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
