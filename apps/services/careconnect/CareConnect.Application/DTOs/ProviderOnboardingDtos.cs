// CC2-INT-B09: DTOs for the provider tenant self-onboarding endpoints.
namespace CareConnect.Application.DTOs;

/// <summary>Request body for POST /api/provider/onboarding/provision-tenant.</summary>
public sealed class ProviderOnboardingRequest
{
    public string TenantName { get; init; } = string.Empty;
    public string TenantCode { get; init; } = string.Empty;
}

/// <summary>Response body for POST /api/provider/onboarding/provision-tenant.</summary>
public sealed class ProviderOnboardingResponse
{
    public Guid   ProviderId         { get; init; }
    public Guid   TenantId           { get; init; }
    public string TenantCode         { get; init; } = string.Empty;
    public string Subdomain          { get; init; } = string.Empty;
    public string ProvisioningStatus { get; init; } = string.Empty;
    public string? PortalUrl         { get; init; }
    public string Message            { get; init; } = string.Empty;

    /// <summary>
    /// BLK-CC-02: True when this request completed a prior partial attempt.
    /// Frontend can display a "workspace setup resumed" message.
    /// </summary>
    public bool IsResumed { get; init; }
}

/// <summary>Response body for GET /api/provider/onboarding/check-code.</summary>
public sealed class TenantCodeAvailabilityResponse
{
    public bool    Available      { get; init; }
    public string  NormalizedCode { get; init; } = string.Empty;
    public string? Message        { get; init; }
}
