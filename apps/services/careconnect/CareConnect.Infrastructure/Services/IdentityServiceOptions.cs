namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Configuration for the Identity service HTTP client used by
/// HttpIdentityMembershipClient and any other cross-service calls.
///
/// Bind from appsettings via:
///   "IdentityService": {
///     "BaseUrl":            "http://identity-service:5001",
///     "TimeoutSeconds":     5,
///     "ProvisioningToken":  "shared-secret"   // sent as X-Provisioning-Token on internal calls
///   }
///
/// BLK-SEC-01: ProvisioningToken replaces the generic AuthHeaderName/AuthHeaderValue pattern
/// for the internal membership API. Must match IdentityService:ProvisioningSecret on the
/// Identity service side (which uses TenantService:ProvisioningSecret as the config key).
/// </summary>
public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    /// <summary>
    /// Base URL of the Identity service, e.g. http://identity:5001 or https://gateway/identity.
    /// When null or empty, the HTTP client returns null without making any network call.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Per-request HTTP timeout in seconds. Defaults to 5 s.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// BLK-SEC-01: Shared provisioning token sent as X-Provisioning-Token header on all
    /// internal membership calls (assign-tenant, assign-roles).
    /// Must match TenantService:ProvisioningSecret on the Identity service side.
    /// When empty, the header is omitted (dev mode — Identity service skips the check).
    /// Set via environment variable / secret — never commit real values to appsettings.
    /// </summary>
    public string? ProvisioningToken { get; set; }

    /// <summary>
    /// Legacy generic auth header name. Retained for backward compatibility.
    /// Prefer ProvisioningToken for internal provisioning calls.
    /// </summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>
    /// Legacy generic auth header value. Retained for backward compatibility.
    /// Prefer ProvisioningToken for internal provisioning calls.
    /// </summary>
    public string? AuthHeaderValue { get; set; }
}
