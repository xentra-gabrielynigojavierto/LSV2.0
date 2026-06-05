using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTemplateAssignmentRepository : ITemplateAssignmentRepository
{
    private readonly List<ReportTemplateAssignment> _assignments = new();

    public Task<ReportTemplateAssignment?> GetByIdAsync(Guid assignmentId, CancellationToken ct)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Id == assignmentId);
        return Task.FromResult(assignment);
    }

    public Task<IReadOnlyList<ReportTemplateAssignment>> ListByTemplateAsync(Guid templateId, CancellationToken ct)
    {
        var list = _assignments
            .Where(a => a.ReportTemplateId == templateId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<ReportTemplateAssignment>>(list.AsReadOnly());
    }

    public Task<ReportTemplateAssignment> CreateAsync(ReportTemplateAssignment assignment, CancellationToken ct)
    {
        if (assignment.Id == Guid.Empty) assignment.Id = Guid.NewGuid();
        _assignments.Add(assignment);
        return Task.FromResult(assignment);
    }

    public Task<ReportTemplateAssignment> UpdateAsync(ReportTemplateAssignment assignment, CancellationToken ct)
    {
        var idx = _assignments.FindIndex(a => a.Id == assignment.Id);
        if (idx >= 0) _assignments[idx] = assignment;
        return Task.FromResult(assignment);
    }

    public Task<bool> HasActiveGlobalAssignmentAsync(Guid templateId, Guid? excludeAssignmentId, CancellationToken ct)
    {
        var exists = _assignments.Any(a =>
            a.ReportTemplateId == templateId
            && a.AssignmentScope == "Global"
            && a.IsActive
            && (!excludeAssignmentId.HasValue || a.Id != excludeAssignmentId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> HasActiveTenantAssignmentAsync(Guid templateId, string tenantId, Guid? excludeAssignmentId, CancellationToken ct)
    {
        var exists = _assignments.Any(a =>
            a.ReportTemplateId == templateId
            && a.AssignmentScope == "Tenant"
            && a.IsActive
            && a.TenantTargets.Any(t => t.TenantId == tenantId && t.IsActive)
            && (!excludeAssignmentId.HasValue || a.Id != excludeAssignmentId.Value));
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<ReportTemplateAssignmentTenant>> ListTenantTargetsAsync(Guid assignmentId, CancellationToken ct)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Id == assignmentId);
        var list = assignment?.TenantTargets.OrderBy(t => t.TenantId).ToList()
                   ?? new List<ReportTemplateAssignmentTenant>();
        return Task.FromResult<IReadOnlyList<ReportTemplateAssignmentTenant>>(list.AsReadOnly());
    }

    public Task<IReadOnlyList<ReportTemplate>> ResolveTenantCatalogAsync(string tenantId, string productCode, string organizationType, CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<ReportTemplate>>(new List<ReportTemplate>().AsReadOnly());
    }
}
