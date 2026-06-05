using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface ITemplateService
{
    Task<TemplateDto?> GetByIdAsync(Guid id);
    Task<List<TemplateDto>> ListByTenantAsync(Guid? tenantId, int limit = 50, int offset = 0);
    Task<List<TemplateDto>> ListGlobalAsync(int limit = 50, int offset = 0);
    Task<TemplateDto> CreateAsync(Guid? tenantId, CreateTemplateDto request);
    Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateDto request);
    Task DeleteAsync(Guid id);
    Task<TemplateVersionDto> CreateVersionAsync(Guid templateId, CreateTemplateVersionDto request);
    Task<TemplateVersionDto> PublishVersionAsync(Guid templateId, Guid versionId, string? publishedBy);
    Task<List<TemplateVersionDto>> ListVersionsAsync(Guid templateId);
}
