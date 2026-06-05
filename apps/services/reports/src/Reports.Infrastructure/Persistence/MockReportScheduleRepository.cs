using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockReportScheduleRepository : IReportScheduleRepository
{
    private readonly List<ReportSchedule> _schedules = new();
    private readonly List<ReportScheduleRun> _runs = new();
    private readonly object _lock = new();

    public Task<ReportSchedule> SaveAsync(ReportSchedule schedule, CancellationToken ct)
    {
        lock (_lock)
        {
            if (schedule.Id == Guid.Empty)
                schedule.Id = Guid.NewGuid();
            _schedules.Add(schedule);
        }
        return Task.FromResult(schedule);
    }

    public Task<ReportSchedule> UpdateAsync(ReportSchedule schedule, CancellationToken ct)
    {
        lock (_lock)
        {
            var idx = _schedules.FindIndex(s => s.Id == schedule.Id);
            if (idx >= 0) _schedules[idx] = schedule;
        }
        return Task.FromResult(schedule);
    }

    public Task<ReportSchedule?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        lock (_lock) { return Task.FromResult(_schedules.FirstOrDefault(s => s.Id == id)); }
    }

    public Task<IReadOnlyList<ReportSchedule>> ListByTenantAsync(
        string tenantId, string? productCode, int page, int pageSize, CancellationToken ct)
    {
        lock (_lock)
        {
            var query = _schedules.Where(s => s.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(productCode))
                query = query.Where(s => s.ProductCode == productCode);

            IReadOnlyList<ReportSchedule> result = query
                .OrderByDescending(s => s.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<ReportSchedule>> GetDueSchedulesAsync(
        DateTimeOffset asOfUtc, int maxCount, CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<ReportSchedule> result = _schedules
                .Where(s => s.IsActive && s.NextRunAtUtc != null && s.NextRunAtUtc <= asOfUtc)
                .OrderBy(s => s.NextRunAtUtc)
                .Take(maxCount)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<ReportScheduleRun> SaveRunAsync(ReportScheduleRun run, CancellationToken ct)
    {
        lock (_lock)
        {
            if (run.Id == Guid.Empty)
                run.Id = Guid.NewGuid();
            _runs.Add(run);
        }
        return Task.FromResult(run);
    }

    public Task<ReportScheduleRun> UpdateRunAsync(ReportScheduleRun run, CancellationToken ct)
    {
        lock (_lock)
        {
            var idx = _runs.FindIndex(r => r.Id == run.Id);
            if (idx >= 0) _runs[idx] = run;
        }
        return Task.FromResult(run);
    }

    public Task<ReportScheduleRun?> GetRunByIdAsync(Guid runId, CancellationToken ct)
    {
        lock (_lock) { return Task.FromResult(_runs.FirstOrDefault(r => r.Id == runId)); }
    }

    public Task<IReadOnlyList<ReportScheduleRun>> ListRunsByScheduleAsync(
        Guid scheduleId, int page, int pageSize, CancellationToken ct)
    {
        lock (_lock)
        {
            IReadOnlyList<ReportScheduleRun> result = _runs
                .Where(r => r.ReportScheduleId == scheduleId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
