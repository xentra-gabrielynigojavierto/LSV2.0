using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface IReportScheduleRepository
{
    Task<ReportSchedule> SaveAsync(ReportSchedule schedule, CancellationToken ct = default);
    Task<ReportSchedule> UpdateAsync(ReportSchedule schedule, CancellationToken ct = default);
    Task<ReportSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportSchedule>> ListByTenantAsync(string tenantId, string? productCode = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ReportSchedule>> GetDueSchedulesAsync(DateTimeOffset asOfUtc, int maxCount, CancellationToken ct = default);
    Task<ReportScheduleRun> SaveRunAsync(ReportScheduleRun run, CancellationToken ct = default);
    Task<ReportScheduleRun> UpdateRunAsync(ReportScheduleRun run, CancellationToken ct = default);
    Task<ReportScheduleRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportScheduleRun>> ListRunsByScheduleAsync(Guid scheduleId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
