using Reports.Application.Scheduling.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Application.Scheduling;

public interface IReportScheduleService
{
    Task<ServiceResult<ReportScheduleResponse>> CreateScheduleAsync(CreateReportScheduleRequest request, CancellationToken ct = default);
    Task<ServiceResult<ReportScheduleResponse>> UpdateScheduleAsync(Guid scheduleId, UpdateReportScheduleRequest request, CancellationToken ct = default);
    Task<ServiceResult<ReportScheduleResponse>> GetScheduleByIdAsync(Guid scheduleId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ReportScheduleResponse>>> ListSchedulesAsync(string tenantId, string? productCode, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<ServiceResult<ReportScheduleResponse>> DeactivateScheduleAsync(Guid scheduleId, CancellationToken ct = default);
    Task<ServiceResult<ReportScheduleRunResponse>> TriggerRunNowAsync(Guid scheduleId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ReportScheduleRunResponse>>> ListRunsAsync(Guid scheduleId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ServiceResult<ReportScheduleRunResponse>> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task ProcessDueSchedulesAsync(CancellationToken ct = default);
}
