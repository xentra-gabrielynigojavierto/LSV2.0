using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Domain.Enums;

namespace Comms.Application.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IEmailTemplateConfigRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IEmailTemplateConfigRepository repo,
        IAuditPublisher audit,
        ILogger<EmailTemplateService> logger)
    {
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<EmailTemplateConfigResponse> CreateAsync(
        CreateEmailTemplateConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default)
    {
        if (request.TemplateScope == TemplateScope.Global)
            throw new UnauthorizedAccessException("Global templates can only be created by platform administrators.");

        var effectiveTenantId = (Guid?)tenantId;

        var config = EmailTemplateConfig.Create(
            request.TemplateKey,
            request.DisplayName,
            request.TemplateScope,
            userId,
            effectiveTenantId,
            request.SubjectTemplate,
            request.BodyTextTemplate,
            request.BodyHtmlTemplate,
            request.IsDefault);

        await _repo.AddAsync(config, ct);
        await _repo.SaveChangesAsync(ct);

        _audit.Publish("TemplateCreated", "Created",
            $"Email template created: {config.DisplayName} (key: {config.TemplateKey})",
            tenantId, userId, "EmailTemplateConfig", config.Id.ToString(),
            metadata: $"{{\"templateKey\":\"{config.TemplateKey}\",\"templateScope\":\"{config.TemplateScope}\",\"isDefault\":{config.IsDefault.ToString().ToLower()}}}");

        _logger.LogInformation("Email template {Id} created: key={Key}, scope={Scope}",
            config.Id, config.TemplateKey, config.TemplateScope);

        return ToResponse(config);
    }

    public async Task<EmailTemplateConfigResponse> UpdateAsync(
        Guid id, UpdateEmailTemplateConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Email template '{id}' not found.");

        if (config.TenantId.HasValue && config.TenantId.Value != tenantId)
            throw new UnauthorizedAccessException("Cannot update templates belonging to another tenant.");

        if (!config.TenantId.HasValue)
            throw new UnauthorizedAccessException("Global templates can only be updated by platform administrators.");

        config.Update(
            request.DisplayName,
            request.SubjectTemplate,
            request.BodyTextTemplate,
            request.BodyHtmlTemplate,
            request.IsDefault,
            request.IsActive,
            userId);

        await _repo.SaveChangesAsync(ct);

        _audit.Publish("TemplateUpdated", "Updated",
            $"Email template updated: {config.DisplayName} (key: {config.TemplateKey}, v{config.Version})",
            tenantId, userId, "EmailTemplateConfig", config.Id.ToString(),
            metadata: $"{{\"templateKey\":\"{config.TemplateKey}\",\"version\":{config.Version}}}");

        return ToResponse(config);
    }

    public async Task<EmailTemplateConfigResponse?> GetByIdAsync(
        Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(id, ct);
        if (config is null) return null;
        if (config.TenantId.HasValue && config.TenantId.Value != tenantId) return null;
        return ToResponse(config);
    }

    public async Task<List<EmailTemplateConfigResponse>> ListAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var configs = await _repo.ListByTenantAsync(tenantId, ct);
        return configs.Select(ToResponse).ToList();
    }

    private static EmailTemplateConfigResponse ToResponse(EmailTemplateConfig c) => new(
        c.Id, c.TenantId, c.TemplateKey, c.DisplayName,
        c.SubjectTemplate, c.BodyTextTemplate, c.BodyHtmlTemplate,
        c.TemplateScope, c.IsDefault, c.IsActive, c.Version,
        c.CreatedAtUtc, c.UpdatedAtUtc);
}
