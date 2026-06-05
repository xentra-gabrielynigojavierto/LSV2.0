using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class TemplateResolutionService : ITemplateResolutionService
{
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateVersionRepository _versionRepo;
    private readonly ILogger<TemplateResolutionService> _logger;

    public TemplateResolutionService(ITemplateRepository templateRepo, ITemplateVersionRepository versionRepo, ILogger<TemplateResolutionService> logger)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _logger = logger;
    }

    public async Task<ResolvedTemplate?> ResolveAsync(Guid tenantId, string templateKey, string channel)
    {
        var tenantTemplate = await _templateRepo.FindByKeyAsync(templateKey, channel, tenantId);
        if (tenantTemplate is { Status: "active" })
        {
            var version = await _versionRepo.FindPublishedByTemplateIdAsync(tenantTemplate.Id);
            if (version != null)
            {
                _logger.LogDebug("Template resolved via tenant-specific override: {TemplateKey} {Channel} {TenantId}", templateKey, channel, tenantId);
                return new ResolvedTemplate { Template = tenantTemplate, Version = version };
            }
        }

        var globalTemplate = await _templateRepo.FindByKeyAsync(templateKey, channel, null);
        if (globalTemplate is { Status: "active" })
        {
            var version = await _versionRepo.FindPublishedByTemplateIdAsync(globalTemplate.Id);
            if (version != null)
            {
                _logger.LogDebug("Template resolved via global system template: {TemplateKey} {Channel}", templateKey, channel);
                return new ResolvedTemplate { Template = globalTemplate, Version = version };
            }
        }

        _logger.LogWarning("Template resolution failed - no active+published template found: {TemplateKey} {Channel} {TenantId}", templateKey, channel, tenantId);
        return null;
    }

    public async Task<ResolvedTemplate?> ResolveByProductAsync(Guid tenantId, string templateKey, string channel, string productType)
    {
        var globalTemplate = await _templateRepo.FindGlobalByProductKeyAsync(productType, channel, templateKey, "global");
        if (globalTemplate is { Status: "active" })
        {
            var version = await _versionRepo.FindPublishedByTemplateIdAsync(globalTemplate.Id);
            if (version != null)
            {
                _logger.LogDebug("Template resolved via product-type global template: {TemplateKey} {Channel} {ProductType}", templateKey, channel, productType);
                return new ResolvedTemplate { Template = globalTemplate, Version = version };
            }
        }

        return await ResolveAsync(tenantId, templateKey, channel);
    }
}
