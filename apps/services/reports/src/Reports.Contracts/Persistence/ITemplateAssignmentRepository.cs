using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITemplateAssignmentRepository
{
    Task<ReportTemplateAssignment?> GetByIdAsync(Guid assignmentId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplateAssignment>> ListByTemplateAsync(Guid templateId, CancellationToken ct = default);
    Task<ReportTemplateAssignment> CreateAsync(ReportTemplateAssignment assignment, CancellationToken ct = default);
    Task<ReportTemplateAssignment> UpdateAsync(ReportTemplateAssignment assignment, CancellationToken ct = default);
    Task<bool> HasActiveGlobalAssignmentAsync(Guid templateId, Guid? excludeAssignmentId, CancellationToken ct = default);
    Task<bool> HasActiveTenantAssignmentAsync(Guid templateId, string tenantId, Guid? excludeAssignmentId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplateAssignmentTenant>> ListTenantTargetsAsync(Guid assignmentId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplate>> ResolveTenantCatalogAsync(string tenantId, string productCode, string organizationType, CancellationToken ct = default);
}
