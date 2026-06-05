namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Configuration for the Tenant service HTTP client used during provider onboarding.
///
/// Bind from appsettings via:
///   "TenantService": {
///     "BaseUrl":           "http://tenant-service:5005",
///     "TimeoutSeconds":    10,
///     "ProvisioningToken": "shared-secret"   // optional; passed as X-Provisioning-Token
///   }
/// </summary>
public sealed class TenantServiceOptions
{
    public const string SectionName = "TenantService";

    /// <summary>
    /// Base URL of the Tenant service.
    /// When null or empty, all calls are skipped and null is returned.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Per-request HTTP timeout in seconds. Defaults to 10 s.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Shared provisioning token sent as X-Provisioning-Token header.
    /// Must match TenantService:ProvisioningSecret on the Tenant service side.
    /// When empty, the header is omitted (dev mode — Tenant service skips the check).
    /// </summary>
    public string? ProvisioningToken { get; set; }
}
