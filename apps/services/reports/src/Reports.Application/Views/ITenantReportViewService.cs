using Reports.Application.Templates.DTOs;
using Reports.Application.Views.DTOs;

namespace Reports.Application.Views;

public interface ITenantReportViewService
{
    Task<ServiceResult<TenantReportViewResponse>> CreateViewAsync(Guid templateId, CreateTenantReportViewRequest request, CancellationToken ct);
    Task<ServiceResult<TenantReportViewResponse>> UpdateViewAsync(Guid templateId, Guid viewId, UpdateTenantReportViewRequest request, CancellationToken ct);
    Task<ServiceResult<TenantReportViewResponse>> GetViewByIdAsync(Guid templateId, Guid viewId, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<TenantReportViewResponse>>> ListViewsAsync(Guid templateId, string tenantId, CancellationToken ct);
    Task<ServiceResult<TenantReportViewResponse>> DeleteViewAsync(Guid templateId, Guid viewId, CancellationToken ct);
}
