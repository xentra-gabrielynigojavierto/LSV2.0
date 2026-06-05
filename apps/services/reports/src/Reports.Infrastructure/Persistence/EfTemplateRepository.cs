using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfTemplateRepository : ITemplateRepository
{
    private readonly ReportsDbContext _db;

    public EfTemplateRepository(ReportsDbContext db) => _db = db;

    public async Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.ReportTemplates
            .Include(t => t.Versions.Where(v => v.IsActive))
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<ReportTemplate?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await _db.ReportTemplates
            .Include(t => t.Versions.Where(v => v.IsActive))
            .FirstOrDefaultAsync(t => t.Code == code, ct);
    }

    public async Task<IReadOnlyList<ReportTemplate>> ListAsync(string? productCode, string? organizationType, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.ReportTemplates.AsQueryable();

        if (!string.IsNullOrEmpty(productCode))
            query = query.Where(t => t.ProductCode == productCode);

        if (!string.IsNullOrEmpty(organizationType))
            query = query.Where(t => t.OrganizationType == organizationType);

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<ReportTemplate> CreateAsync(ReportTemplate template, CancellationToken ct)
    {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();

        _db.ReportTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<ReportTemplate> UpdateAsync(ReportTemplate template, CancellationToken ct)
    {
        _db.ReportTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<ReportTemplateVersion?> GetVersionAsync(Guid templateId, int versionNumber, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .FirstOrDefaultAsync(v => v.ReportTemplateId == templateId && v.VersionNumber == versionNumber, ct);
    }

    public async Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid templateId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId && v.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ReportTemplateVersion?> GetPublishedVersionAsync(Guid templateId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId && v.IsPublished)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ReportTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();

        _db.ReportTemplateVersions.Add(version);
        await _db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<ReportTemplateVersion> UpdateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        _db.ReportTemplateVersions.Update(version);
        await _db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<ReportTemplateVersion> PublishVersionAtomicAsync(Guid templateId, int versionNumber, string publishedByUserId, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var currentPublished = await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId && v.IsPublished)
            .ToListAsync(ct);

        foreach (var v in currentPublished)
        {
            v.IsPublished = false;
            v.PublishedAtUtc = null;
            v.PublishedByUserId = null;
        }

        var target = await _db.ReportTemplateVersions
            .FirstAsync(v => v.ReportTemplateId == templateId && v.VersionNumber == versionNumber, ct);

        target.IsPublished = true;
        target.PublishedAtUtc = DateTimeOffset.UtcNow;
        target.PublishedByUserId = publishedByUserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return target;
    }

    public async Task<ReportTemplateVersion> CreateVersionAtomicAsync(ReportTemplate template, ReportTemplateVersion version, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var latest = await _db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == template.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var nextVersion = (latest?.VersionNumber ?? 0) + 1;
        version.VersionNumber = nextVersion;

        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();

        _db.ReportTemplateVersions.Add(version);

        template.CurrentVersion = nextVersion;
        _db.ReportTemplates.Update(template);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return version;
    }
}
