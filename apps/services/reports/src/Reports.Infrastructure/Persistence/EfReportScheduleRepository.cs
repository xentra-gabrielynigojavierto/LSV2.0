using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfReportScheduleRepository : IReportScheduleRepository
{
    private readonly ReportsDbContext _db;

    public EfReportScheduleRepository(ReportsDbContext db) => _db = db;

    public async Task<ReportSchedule> SaveAsync(ReportSchedule schedule, CancellationToken ct)
    {
        if (schedule.Id == Guid.Empty)
            schedule.Id = Guid.NewGuid();

        _db.ReportSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);
        return schedule;
    }

    public async Task<ReportSchedule> UpdateAsync(ReportSchedule schedule, CancellationToken ct)
    {
        _db.ReportSchedules.Update(schedule);
        await _db.SaveChangesAsync(ct);
        return schedule;
    }

    public async Task<ReportSchedule?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.ReportSchedules
            .Include(s => s.ReportTemplate)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<ReportSchedule>> ListByTenantAsync(
        string tenantId, string? productCode, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.ReportSchedules
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(productCode))
            query = query.Where(s => s.ProductCode == productCode);

        return await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportSchedule>> GetDueSchedulesAsync(
        DateTimeOffset asOfUtc, int maxCount, CancellationToken ct)
    {
        return await _db.ReportSchedules
            .Where(s => s.IsActive && s.NextRunAtUtc != null && s.NextRunAtUtc <= asOfUtc)
            .OrderBy(s => s.NextRunAtUtc)
            .Take(maxCount)
            .Include(s => s.ReportTemplate)
            .ToListAsync(ct);
    }

    public async Task<ReportScheduleRun> SaveRunAsync(ReportScheduleRun run, CancellationToken ct)
    {
        if (run.Id == Guid.Empty)
            run.Id = Guid.NewGuid();

        _db.ReportScheduleRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<ReportScheduleRun> UpdateRunAsync(ReportScheduleRun run, CancellationToken ct)
    {
        _db.ReportScheduleRuns.Update(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<ReportScheduleRun?> GetRunByIdAsync(Guid runId, CancellationToken ct)
    {
        return await _db.ReportScheduleRuns
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public async Task<IReadOnlyList<ReportScheduleRun>> ListRunsByScheduleAsync(
        Guid scheduleId, int page, int pageSize, CancellationToken ct)
    {
        return await _db.ReportScheduleRuns
            .Where(r => r.ReportScheduleId == scheduleId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
