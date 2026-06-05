using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfReportRepository : IReportRepository
{
    private readonly ReportsDbContext _db;

    public EfReportRepository(ReportsDbContext db) => _db = db;

    public async Task<ReportExecution> SaveAsync(ReportExecution execution, CancellationToken ct)
    {
        if (execution.Id == Guid.Empty)
            execution.Id = Guid.NewGuid();

        _db.ReportExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);
        return execution;
    }

    public async Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.ReportExecutions
            .Include(e => e.ReportTemplate)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<ReportExecution>> ListByTenantAsync(string tenantId, int page, int pageSize, CancellationToken ct)
    {
        return await _db.ReportExecutions
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<ReportExecution> UpdateAsync(ReportExecution execution, CancellationToken ct)
    {
        _db.ReportExecutions.Update(execution);
        await _db.SaveChangesAsync(ct);
        return execution;
    }
}
