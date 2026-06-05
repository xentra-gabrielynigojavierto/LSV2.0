using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfTemplateAssignmentRepository : ITemplateAssignmentRepository
{
    private readonly ReportsDbContext _db;

    public EfTemplateAssignmentRepository(ReportsDbContext db) => _db = db;

    public async Task<ReportTemplateAssignment?> GetByIdAsync(Guid assignmentId, CancellationToken ct)
    {
        return await _db.ReportTemplateAssignments
            .Include(a => a.TenantTargets)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
    }

    public async Task<IReadOnlyList<ReportTemplateAssignment>> ListByTemplateAsync(Guid templateId, CancellationToken ct)
    {
        return await _db.ReportTemplateAssignments
            .Include(a => a.TenantTargets)
            .Where(a => a.ReportTemplateId == templateId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ReportTemplateAssignment> CreateAsync(ReportTemplateAssignment assignment, CancellationToken ct)
    {
        _db.ReportTemplateAssignments.Add(assignment);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("A conflicting assignment or tenant target already exists.", ex);
        }
        return assignment;
    }

    public async Task<ReportTemplateAssignment> UpdateAsync(ReportTemplateAssignment assignment, CancellationToken ct)
    {
        var existingTenants = await _db.ReportTemplateAssignmentTenants
            .Where(t => t.ReportTemplateAssignmentId == assignment.Id)
            .ToListAsync(ct);

        _db.ReportTemplateAssignmentTenants.RemoveRange(existingTenants);

        _db.ReportTemplateAssignments.Update(assignment);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("A conflicting assignment or tenant target already exists.", ex);
        }
        return assignment;
    }

    public async Task<bool> HasActiveGlobalAssignmentAsync(Guid templateId, Guid? excludeAssignmentId, CancellationToken ct)
    {
        var query = _db.ReportTemplateAssignments
            .Where(a => a.ReportTemplateId == templateId
                        && a.AssignmentScope == "Global"
                        && a.IsActive);

        if (excludeAssignmentId.HasValue)
            query = query.Where(a => a.Id != excludeAssignmentId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task<bool> HasActiveTenantAssignmentAsync(Guid templateId, string tenantId, Guid? excludeAssignmentId, CancellationToken ct)
    {
        var query = _db.ReportTemplateAssignments
            .Where(a => a.ReportTemplateId == templateId
                        && a.AssignmentScope == "Tenant"
                        && a.IsActive
                        && a.TenantTargets.Any(t => t.TenantId == tenantId && t.IsActive));

        if (excludeAssignmentId.HasValue)
            query = query.Where(a => a.Id != excludeAssignmentId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<ReportTemplateAssignmentTenant>> ListTenantTargetsAsync(Guid assignmentId, CancellationToken ct)
    {
        return await _db.ReportTemplateAssignmentTenants
            .Where(t => t.ReportTemplateAssignmentId == assignmentId)
            .OrderBy(t => t.TenantId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportTemplate>> ResolveTenantCatalogAsync(
        string tenantId, string productCode, string organizationType, CancellationToken ct)
    {
        return await _db.ReportTemplates
            .Include(t => t.Versions.Where(v => v.IsPublished))
            .Include(t => t.Assignments.Where(a =>
                a.IsActive
                && a.ProductCode == productCode
                && a.OrganizationType == organizationType
                && (a.AssignmentScope == "Global"
                    || a.TenantTargets.Any(tt => tt.TenantId == tenantId && tt.IsActive))))
            .Where(t => t.IsActive
                        && t.ProductCode == productCode
                        && t.OrganizationType == organizationType
                        && t.Assignments.Any(a =>
                            a.IsActive
                            && a.ProductCode == productCode
                            && a.OrganizationType == organizationType
                            && (a.AssignmentScope == "Global"
                                || a.TenantTargets.Any(tt => tt.TenantId == tenantId && tt.IsActive))))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }
}
