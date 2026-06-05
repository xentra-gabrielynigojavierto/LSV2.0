using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(Guid id);
    Task<Template?> FindByKeyAsync(string templateKey, string channel, Guid? tenantId);
    Task<Template?> FindGlobalByProductKeyAsync(string productType, string channel, string templateKey, string scope);
    Task<List<Template>> GetByTenantAsync(Guid? tenantId, int limit = 50, int offset = 0);
    Task<List<Template>> GetGlobalTemplatesAsync(int limit = 50, int offset = 0);
    Task<Template> CreateAsync(Template template);
    Task UpdateAsync(Template template);
    Task DeleteAsync(Guid id);
}

public interface ITemplateVersionRepository
{
    Task<TemplateVersion?> GetByIdAsync(Guid id);
    Task<TemplateVersion?> FindPublishedByTemplateIdAsync(Guid templateId);
    Task<List<TemplateVersion>> GetByTemplateIdAsync(Guid templateId);
    Task<TemplateVersion> CreateAsync(TemplateVersion version);
    Task UpdateAsync(TemplateVersion version);
}
