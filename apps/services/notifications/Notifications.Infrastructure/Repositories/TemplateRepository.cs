using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly NotificationsDbContext _db;
    public TemplateRepository(NotificationsDbContext db) => _db = db;

    public async Task<Template?> GetByIdAsync(Guid id)
        => await _db.Templates.FindAsync(id);

    public async Task<Template?> FindByKeyAsync(string templateKey, string channel, Guid? tenantId)
        => await _db.Templates.FirstOrDefaultAsync(t =>
            t.TemplateKey == templateKey && t.Channel == channel && t.TenantId == tenantId);

    public async Task<Template?> FindGlobalByProductKeyAsync(string productType, string channel, string templateKey, string scope)
        => await _db.Templates.FirstOrDefaultAsync(t =>
            t.ProductType == productType && t.Channel == channel && t.TemplateKey == templateKey && t.Scope == scope && t.TenantId == null);

    public async Task<List<Template>> GetByTenantAsync(Guid? tenantId, int limit = 50, int offset = 0)
        => await _db.Templates.Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt).Skip(offset).Take(limit).ToListAsync();

    public async Task<List<Template>> GetGlobalTemplatesAsync(int limit = 50, int offset = 0)
        => await _db.Templates.Where(t => t.TenantId == null && t.Scope == "global")
            .OrderByDescending(t => t.CreatedAt).Skip(offset).Take(limit).ToListAsync();

    public async Task<Template> CreateAsync(Template template)
    {
        template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(Template template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _db.Templates.Update(template);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var t = await _db.Templates.FindAsync(id);
        if (t != null)
        {
            _db.Templates.Remove(t);
            await _db.SaveChangesAsync();
        }
    }
}

public class TemplateVersionRepository : ITemplateVersionRepository
{
    private readonly NotificationsDbContext _db;
    public TemplateVersionRepository(NotificationsDbContext db) => _db = db;

    public async Task<TemplateVersion?> GetByIdAsync(Guid id)
        => await _db.TemplateVersions.FindAsync(id);

    public async Task<TemplateVersion?> FindPublishedByTemplateIdAsync(Guid templateId)
        => await _db.TemplateVersions.FirstOrDefaultAsync(v => v.TemplateId == templateId && v.IsPublished);

    public async Task<List<TemplateVersion>> GetByTemplateIdAsync(Guid templateId)
        => await _db.TemplateVersions.Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber).ToListAsync();

    public async Task<TemplateVersion> CreateAsync(TemplateVersion version)
    {
        version.Id = version.Id == Guid.Empty ? Guid.NewGuid() : version.Id;
        version.CreatedAt = DateTime.UtcNow;
        version.UpdatedAt = DateTime.UtcNow;
        _db.TemplateVersions.Add(version);
        await _db.SaveChangesAsync();
        return version;
    }

    public async Task UpdateAsync(TemplateVersion version)
    {
        version.UpdatedAt = DateTime.UtcNow;
        _db.TemplateVersions.Update(version);
        await _db.SaveChangesAsync();
    }
}
