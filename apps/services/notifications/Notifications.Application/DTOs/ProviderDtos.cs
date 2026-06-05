namespace Notifications.Application.DTOs;

public class TenantProviderConfigDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    /// <summary>"platform" for platform-level providers, "tenant" for tenant-owned configs.</summary>
    public string OwnershipMode { get; set; } = "tenant";
    public string ValidationStatus { get; set; } = string.Empty;
    public string? ValidationMessage { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime? LastHealthCheckAt { get; set; }
    public int? HealthCheckLatencyMs { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTenantProviderConfigDto
{
    public string Channel { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = "{}";
    public string? SettingsJson { get; set; }
    public int? Priority { get; set; }
}

public class UpdateTenantProviderConfigDto
{
    public string? DisplayName { get; set; }
    public string? CredentialsJson { get; set; }
    public string? SettingsJson { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
}

public class TenantChannelSettingDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ProviderMode { get; set; } = string.Empty;
    public Guid? PrimaryTenantProviderConfigId { get; set; }
    public Guid? FallbackTenantProviderConfigId { get; set; }
    public bool AllowPlatformFallback { get; set; }
    public bool AllowAutomaticFailover { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateChannelSettingDto
{
    public string? ProviderMode { get; set; }
    public Guid? PrimaryTenantProviderConfigId { get; set; }
    public Guid? FallbackTenantProviderConfigId { get; set; }
    public bool? AllowPlatformFallback { get; set; }
    public bool? AllowAutomaticFailover { get; set; }
}
