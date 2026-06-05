using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskTemplateRepository : ITaskTemplateRepository
{
    private readonly TasksDbContext _db;
    public TaskTemplateRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<TaskTemplate?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.Templates
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskTemplate>> GetByTenantAsync(
        Guid tenantId, string? sourceProductCode = null, bool activeOnly = true, CancellationToken ct = default)
    {
        var q = _db.Templates.Where(t => t.TenantId == tenantId);

        if (sourceProductCode is not null)
            q = q.Where(t => t.SourceProductCode == sourceProductCode.ToUpperInvariant()
                           || t.SourceProductCode == null);
        if (activeOnly)
            q = q.Where(t => t.IsActive);

        return await q.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task AddAsync(
        TaskTemplate template, CancellationToken ct = default)
        => await _db.Templates.AddAsync(template, ct);

    public System.Threading.Tasks.Task UpdateAsync(
        TaskTemplate template, CancellationToken ct = default)
    {
        _db.Templates.Update(template);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
