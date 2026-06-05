using Reports.Application.Assignments.DTOs;
using Reports.Application.Templates.DTOs;

namespace Reports.Application.Assignments;

public interface ITemplateAssignmentService
{
    Task<ServiceResult<TemplateAssignmentResponse>> CreateAssignmentAsync(Guid templateId, CreateTemplateAssignmentRequest request, CancellationToken ct = default);
    Task<ServiceResult<TemplateAssignmentResponse>> UpdateAssignmentAsync(Guid templateId, Guid assignmentId, UpdateTemplateAssignmentRequest request, CancellationToken ct = default);
    Task<ServiceResult<TemplateAssignmentResponse>> GetAssignmentByIdAsync(Guid templateId, Guid assignmentId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TemplateAssignmentResponse>>> ListAssignmentsAsync(Guid templateId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<TenantTemplateCatalogItemResponse>>> ResolveTenantCatalogAsync(TenantTemplateCatalogQuery query, CancellationToken ct = default);
}
