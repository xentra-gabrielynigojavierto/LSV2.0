using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

public class TemplateServiceImpl : ITemplateService
{
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateVersionRepository _versionRepo;
    private readonly ILogger<TemplateServiceImpl> _logger;

    public TemplateServiceImpl(ITemplateRepository templateRepo, ITemplateVersionRepository versionRepo, ILogger<TemplateServiceImpl> logger)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _logger = logger;
    }

    public async Task<TemplateDto?> GetByIdAsync(Guid id)
    {
        var t = await _templateRepo.GetByIdAsync(id);
        return t != null ? MapToDto(t) : null;
    }

    public async Task<List<TemplateDto>> ListByTenantAsync(Guid? tenantId, int limit = 50, int offset = 0)
    {
        var list = await _templateRepo.GetByTenantAsync(tenantId, limit, offset);
        return list.Select(MapToDto).ToList();
    }

    public async Task<List<TemplateDto>> ListGlobalAsync(int limit = 50, int offset = 0)
    {
        var list = await _templateRepo.GetGlobalTemplatesAsync(limit, offset);
        return list.Select(MapToDto).ToList();
    }

    public async Task<TemplateDto> CreateAsync(Guid? tenantId, CreateTemplateDto request)
    {
        var template = new Template
        {
            TenantId = tenantId,
            TemplateKey = request.TemplateKey,
            Channel = request.Channel,
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope ?? (tenantId.HasValue ? "tenant" : "global"),
            ProductType = request.ProductType,
            Status = "active"
        };

        template = await _templateRepo.CreateAsync(template);
        _logger.LogInformation("Template created: {TemplateId} {TemplateKey} {Channel}", template.Id, template.TemplateKey, template.Channel);
        return MapToDto(template);
    }

    public async Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateDto request)
    {
        var template = await _templateRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Template {id} not found");

        if (request.Name != null) template.Name = request.Name;
        if (request.Description != null) template.Description = request.Description;
        if (request.Status != null) template.Status = request.Status;

        await _templateRepo.UpdateAsync(template);
        return MapToDto(template);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _templateRepo.DeleteAsync(id);
    }

    public async Task<TemplateVersionDto> CreateVersionAsync(Guid templateId, CreateTemplateVersionDto request)
    {
        var existing = await _versionRepo.GetByTemplateIdAsync(templateId);
        var nextVersion = existing.Count > 0 ? existing.Max(v => v.VersionNumber) + 1 : 1;

        var version = new TemplateVersion
        {
            TemplateId = templateId,
            VersionNumber = nextVersion,
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            TextTemplate = request.TextTemplate,
            EditorType = request.EditorType,
            IsPublished = false
        };

        version = await _versionRepo.CreateAsync(version);
        _logger.LogInformation("Template version created: {VersionId} v{VersionNumber} for template {TemplateId}", version.Id, nextVersion, templateId);
        return MapVersionToDto(version);
    }

    public async Task<TemplateVersionDto> PublishVersionAsync(Guid templateId, Guid versionId, string? publishedBy)
    {
        var versions = await _versionRepo.GetByTemplateIdAsync(templateId);
        foreach (var v in versions.Where(v => v.IsPublished))
        {
            v.IsPublished = false;
            await _versionRepo.UpdateAsync(v);
        }

        var version = await _versionRepo.GetByIdAsync(versionId)
            ?? throw new KeyNotFoundException($"Template version {versionId} not found");

        version.IsPublished = true;
        version.PublishedBy = publishedBy;
        version.PublishedAt = DateTime.UtcNow;
        await _versionRepo.UpdateAsync(version);

        _logger.LogInformation("Template version published: {VersionId} v{VersionNumber} for template {TemplateId}", version.Id, version.VersionNumber, templateId);
        return MapVersionToDto(version);
    }

    public async Task<List<TemplateVersionDto>> ListVersionsAsync(Guid templateId)
    {
        var versions = await _versionRepo.GetByTemplateIdAsync(templateId);
        return versions.Select(MapVersionToDto).ToList();
    }

    private static TemplateDto MapToDto(Template t) => new()
    {
        Id = t.Id, TenantId = t.TenantId, TemplateKey = t.TemplateKey, Channel = t.Channel,
        Name = t.Name, Description = t.Description, Status = t.Status, Scope = t.Scope,
        ProductType = t.ProductType, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt
    };

    private static TemplateVersionDto MapVersionToDto(TemplateVersion v) => new()
    {
        Id = v.Id, TemplateId = v.TemplateId, VersionNumber = v.VersionNumber,
        SubjectTemplate = v.SubjectTemplate, BodyTemplate = v.BodyTemplate, TextTemplate = v.TextTemplate,
        EditorType = v.EditorType, IsPublished = v.IsPublished, PublishedBy = v.PublishedBy,
        PublishedAt = v.PublishedAt, CreatedAt = v.CreatedAt, UpdatedAt = v.UpdatedAt
    };
}
