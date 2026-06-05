using Microsoft.Extensions.Logging;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Domain.Entities;

namespace Comms.Application.Services;

public class SenderConfigService : ISenderConfigService
{
    private readonly ITenantEmailSenderConfigRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<SenderConfigService> _logger;

    public SenderConfigService(
        ITenantEmailSenderConfigRepository repo,
        IAuditPublisher audit,
        ILogger<SenderConfigService> logger)
    {
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<TenantEmailSenderConfigResponse> CreateAsync(
        CreateTenantEmailSenderConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default)
    {
        var config = TenantEmailSenderConfig.Create(
            tenantId,
            request.DisplayName,
            request.FromEmail,
            request.SenderType,
            userId,
            request.ReplyToEmail,
            request.IsDefault,
            request.AllowedForSharedExternal,
            request.VerificationStatus);

        if (config.IsDefault)
        {
            var existingDefaults = await _repo.GetDefaultsAsync(tenantId, ct);
            foreach (var d in existingDefaults)
                d.ClearDefault(userId);
        }

        await _repo.AddAsync(config, ct);
        await _repo.SaveChangesAsync(ct);

        _audit.Publish("SenderConfigCreated", "Created",
            $"Sender config created: {config.DisplayName} <{config.FromEmail}>",
            tenantId, userId, "TenantEmailSenderConfig", config.Id.ToString(),
            metadata: $"{{\"fromEmail\":\"{config.FromEmail}\",\"senderType\":\"{config.SenderType}\",\"isDefault\":{config.IsDefault.ToString().ToLower()},\"verificationStatus\":\"{config.VerificationStatus}\"}}");

        _logger.LogInformation("Sender config {Id} created for tenant {TenantId}: {Email}",
            config.Id, tenantId, config.FromEmail);

        return ToResponse(config);
    }

    public async Task<TenantEmailSenderConfigResponse> UpdateAsync(
        Guid id, UpdateTenantEmailSenderConfigRequest request, Guid tenantId, Guid userId,
        CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Sender config '{id}' not found.");

        var wasDefault = config.IsDefault;

        config.Update(
            request.DisplayName,
            request.ReplyToEmail,
            request.SenderType,
            request.IsDefault,
            request.IsActive,
            request.VerificationStatus,
            request.AllowedForSharedExternal,
            userId);

        if (config.IsDefault && !wasDefault)
        {
            var existingDefaults = await _repo.GetDefaultsAsync(tenantId, ct);
            foreach (var d in existingDefaults.Where(d => d.Id != config.Id))
                d.ClearDefault(userId);

            _audit.Publish("SenderDefaultChanged", "Updated",
                $"Default sender changed to: {config.DisplayName} <{config.FromEmail}>",
                tenantId, userId, "TenantEmailSenderConfig", config.Id.ToString());
        }

        await _repo.SaveChangesAsync(ct);

        _audit.Publish("SenderConfigUpdated", "Updated",
            $"Sender config updated: {config.DisplayName} <{config.FromEmail}>",
            tenantId, userId, "TenantEmailSenderConfig", config.Id.ToString(),
            metadata: $"{{\"fromEmail\":\"{config.FromEmail}\",\"isActive\":{config.IsActive.ToString().ToLower()},\"verificationStatus\":\"{config.VerificationStatus}\"}}");

        return ToResponse(config);
    }

    public async Task<TenantEmailSenderConfigResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(tenantId, id, ct);
        return config is null ? null : ToResponse(config);
    }

    public async Task<List<TenantEmailSenderConfigResponse>> ListAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var configs = await _repo.ListByTenantAsync(tenantId, ct);
        return configs.Select(ToResponse).ToList();
    }

    private static TenantEmailSenderConfigResponse ToResponse(TenantEmailSenderConfig c) => new(
        c.Id, c.TenantId, c.DisplayName, c.FromEmail, c.ReplyToEmail,
        c.SenderType, c.IsDefault, c.IsActive, c.VerificationStatus,
        c.AllowedForSharedExternal, c.CreatedAtUtc, c.UpdatedAtUtc);
}
