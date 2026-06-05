using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskGovernanceRepository : ITaskGovernanceRepository
{
    private readonly TasksDbContext _db;
    public TaskGovernanceRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<TaskGovernanceSettings?> GetByTenantAndProductAsync(
        Guid tenantId, string? sourceProductCode, CancellationToken ct = default)
    {
        var normalized = sourceProductCode?.ToUpperInvariant();
        return await _db.GovernanceSettings
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.SourceProductCode == normalized, ct);
    }

    public async System.Threading.Tasks.Task<TaskGovernanceSettings?> GetTenantDefaultAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _db.GovernanceSettings
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.SourceProductCode == null, ct);

    public async System.Threading.Tasks.Task AddAsync(
        TaskGovernanceSettings settings, CancellationToken ct = default)
        => await _db.GovernanceSettings.AddAsync(settings, ct);
}
