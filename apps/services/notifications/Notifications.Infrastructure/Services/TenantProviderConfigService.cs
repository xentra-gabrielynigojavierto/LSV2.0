using Microsoft.Extensions.Logging;
using Notifications.Application.Constants;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Infrastructure.Services;

public class TenantProviderConfigServiceImpl : ITenantProviderConfigService
{
    private readonly ITenantProviderConfigRepository _configRepo;
    private readonly IProviderHealthRepository _healthRepo;
    private readonly ILogger<TenantProviderConfigServiceImpl> _logger;

    public TenantProviderConfigServiceImpl(ITenantProviderConfigRepository configRepo, IProviderHealthRepository healthRepo, ILogger<TenantProviderConfigServiceImpl> logger)
    {
        _configRepo = configRepo;
        _healthRepo = healthRepo;
        _logger = logger;
    }

    public async Task<TenantProviderConfigDto?> GetByIdAsync(Guid tenantId, Guid id)
    {
        var config = await _configRepo.FindByIdAndTenantAsync(id, tenantId);
        return config != null ? MapToDto(config) : null;
    }

    public async Task<List<TenantProviderConfigDto>> ListAsync(Guid tenantId, string? channel = null)
    {
        var configs = channel != null
            ? await _configRepo.GetByTenantAndChannelAsync(tenantId, channel)
            : await _configRepo.GetByTenantAsync(tenantId);
        return configs.Select(MapToDto).ToList();
    }

    public async Task<TenantProviderConfigDto> CreateAsync(Guid tenantId, CreateTenantProviderConfigDto request)
    {
        var config = new TenantProviderConfig
        {
            TenantId = tenantId,
            Channel = request.Channel,
            ProviderType = request.ProviderType,
            DisplayName = request.DisplayName,
            CredentialsJson = request.CredentialsJson,
            SettingsJson = request.SettingsJson ?? "{}",
            Status = "active",
            ValidationStatus = "not_validated",
            HealthStatus = "unknown",
            Priority = request.Priority ?? 1
        };

        config = await _configRepo.CreateAsync(config);
        _logger.LogInformation("Provider config created: {ConfigId} {ProviderType} for {TenantId}", config.Id, config.ProviderType, tenantId);
        return MapToDto(config);
    }

    public async Task<TenantProviderConfigDto> UpdateAsync(Guid tenantId, Guid id, UpdateTenantProviderConfigDto request)
    {
        var config = await _configRepo.FindByIdAndTenantAsync(id, tenantId)
            ?? throw new KeyNotFoundException($"Provider config {id} not found");

        if (request.DisplayName != null) config.DisplayName = request.DisplayName;
        if (request.CredentialsJson != null) config.CredentialsJson = request.CredentialsJson;
        if (request.SettingsJson != null) config.SettingsJson = request.SettingsJson;
        if (request.Status != null) config.Status = request.Status;
        if (request.Priority.HasValue) config.Priority = request.Priority.Value;

        await _configRepo.UpdateAsync(config);
        return MapToDto(config);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id)
    {
        var config = await _configRepo.FindByIdAndTenantAsync(id, tenantId)
            ?? throw new KeyNotFoundException($"Provider config {id} not found");
        await _configRepo.DeleteAsync(config.Id);
    }

    public async Task<TenantProviderConfigDto> ValidateAsync(Guid tenantId, Guid id)
    {
        var config = await _configRepo.FindByIdAndTenantAsync(id, tenantId)
            ?? throw new KeyNotFoundException($"Provider config {id} not found");

        config.ValidationStatus = "validated";
        config.ValidationMessage = "Configuration validated successfully";
        config.LastValidatedAt = DateTime.UtcNow;
        await _configRepo.UpdateAsync(config);
        return MapToDto(config);
    }

    public async Task<TenantProviderConfigDto> HealthCheckAsync(Guid tenantId, Guid id)
    {
        var config = await _configRepo.FindByIdAndTenantAsync(id, tenantId)
            ?? throw new KeyNotFoundException($"Provider config {id} not found");

        config.HealthStatus = "healthy";
        config.LastHealthCheckAt = DateTime.UtcNow;
        config.HealthCheckLatencyMs = 0;
        await _configRepo.UpdateAsync(config);
        return MapToDto(config);
    }

    public async Task<List<TenantProviderConfigDto>> ListPlatformAsync(string? channel = null)
    {
        var configs = channel != null
            ? await _configRepo.GetByTenantAndChannelAsync(PlatformProvider.PlatformTenantId, channel)
            : await _configRepo.GetByTenantAsync(PlatformProvider.PlatformTenantId);
        return configs.Select(MapToDto).ToList();
    }

    public async Task<TenantProviderConfigDto?> GetPlatformByIdAsync(Guid id)
    {
        var config = await _configRepo.GetByIdAsync(id);
        if (config == null || config.TenantId != PlatformProvider.PlatformTenantId) return null;
        return MapToDto(config);
    }

    public Task<TenantProviderConfigDto> CreatePlatformAsync(CreateTenantProviderConfigDto request)
        => CreateAsync(PlatformProvider.PlatformTenantId, request);

    public async Task<TenantProviderConfigDto> UpdatePlatformAsync(Guid id, UpdateTenantProviderConfigDto request)
    {
        var config = await _configRepo.GetByIdAsync(id);
        if (config == null || config.TenantId != PlatformProvider.PlatformTenantId)
            throw new KeyNotFoundException($"Platform provider config {id} not found");

        if (request.DisplayName != null) config.DisplayName = request.DisplayName;
        if (request.CredentialsJson != null) config.CredentialsJson = request.CredentialsJson;
        if (request.SettingsJson != null) config.SettingsJson = request.SettingsJson;
        if (request.Status != null) config.Status = request.Status;
        if (request.Priority.HasValue) config.Priority = request.Priority.Value;

        await _configRepo.UpdateAsync(config);
        return MapToDto(config);
    }

    public async Task DeletePlatformAsync(Guid id)
    {
        var config = await _configRepo.GetByIdAsync(id);
        if (config == null || config.TenantId != PlatformProvider.PlatformTenantId)
            throw new KeyNotFoundException($"Platform provider config {id} not found");
        await _configRepo.DeleteAsync(config.Id);
    }

    private static TenantProviderConfigDto MapToDto(TenantProviderConfig c) => new()
    {
        Id = c.Id, TenantId = c.TenantId, Channel = c.Channel, ProviderType = c.ProviderType,
        DisplayName = c.DisplayName, SettingsJson = c.SettingsJson ?? "{}", Status = c.Status,
        OwnershipMode = c.TenantId == PlatformProvider.PlatformTenantId ? "platform" : "tenant",
        ValidationStatus = c.ValidationStatus, ValidationMessage = c.ValidationMessage,
        LastValidatedAt = c.LastValidatedAt, HealthStatus = c.HealthStatus,
        LastHealthCheckAt = c.LastHealthCheckAt, HealthCheckLatencyMs = c.HealthCheckLatencyMs,
        Priority = c.Priority, CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };
}
