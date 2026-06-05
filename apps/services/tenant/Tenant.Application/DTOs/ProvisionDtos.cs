namespace Tenant.Application.DTOs;

/// <summary>
/// Response for GET /api/v1/tenants/check-code?code=acme
/// </summary>
public record CheckCodeResponse(
    bool    Available,
    string  NormalizedCode,
    string? Error = null);

/// <summary>
/// Request for POST /api/v1/tenants/provision
/// Minimal surface: caller supplies only the tenant name and desired code.
/// Subdomain defaults to the normalized code.
/// </summary>
public record ProvisionRequest(
    string TenantName,
    string TenantCode);

/// <summary>
/// Response for POST /api/v1/tenants/provision
/// </summary>
public record ProvisionResponse(
    Guid   TenantId,
    string TenantCode,
    string Subdomain);
