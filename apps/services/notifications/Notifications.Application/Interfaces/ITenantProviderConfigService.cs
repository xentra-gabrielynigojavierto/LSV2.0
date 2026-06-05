using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface ITenantProviderConfigService
{
    Task<TenantProviderConfigDto?> GetByIdAsync(Guid tenantId, Guid id);
    Task<List<TenantProviderConfigDto>> ListAsync(Guid tenantId, string? channel = null);
    Task<TenantProviderConfigDto> CreateAsync(Guid tenantId, CreateTenantProviderConfigDto request);
    Task<TenantProviderConfigDto> UpdateAsync(Guid tenantId, Guid id, UpdateTenantProviderConfigDto request);
    Task DeleteAsync(Guid tenantId, Guid id);
    Task<TenantProviderConfigDto> ValidateAsync(Guid tenantId, Guid id);
    Task<TenantProviderConfigDto> HealthCheckAsync(Guid tenantId, Guid id);

    // ── Platform-level provider access (no tenant scope required) ───────────────
    /// <summary>Lists all platform-owned provider configs (i.e. not tied to any tenant).</summary>
    Task<List<TenantProviderConfigDto>> ListPlatformAsync(string? channel = null);
    /// <summary>Gets a platform-owned config by its ID, or null if it does not exist or belongs to a tenant.</summary>
    Task<TenantProviderConfigDto?> GetPlatformByIdAsync(Guid id);
    /// <summary>Creates a platform-owned provider config (TenantId = PlatformTenantId sentinel).</summary>
    Task<TenantProviderConfigDto> CreatePlatformAsync(CreateTenantProviderConfigDto request);
    /// <summary>Updates a platform-owned provider config by ID.</summary>
    Task<TenantProviderConfigDto> UpdatePlatformAsync(Guid id, UpdateTenantProviderConfigDto request);
    /// <summary>Deletes a platform-owned provider config by ID.</summary>
    Task DeletePlatformAsync(Guid id);
}
