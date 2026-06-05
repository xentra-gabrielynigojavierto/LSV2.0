using Reports.Application.Export.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Application.Export;

public interface IReportExportService
{
    Task<ServiceResult<ExportReportResponse>> ExportReportAsync(ExportReportRequest request, CancellationToken ct);
}
