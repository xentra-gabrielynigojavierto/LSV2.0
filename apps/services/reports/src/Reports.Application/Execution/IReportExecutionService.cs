using Reports.Application.Execution.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Application.Execution;

public interface IReportExecutionService
{
    Task<ServiceResult<ReportExecutionResponse>> ExecuteReportAsync(ExecuteReportRequest request, CancellationToken ct);
    Task<ServiceResult<ReportExecutionSummaryResponse>> GetExecutionByIdAsync(Guid executionId, CancellationToken ct);
}
