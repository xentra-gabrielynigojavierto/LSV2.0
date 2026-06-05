namespace Identity.Application.DTOs;

/// <summary>
/// Response from GET /api/tenants/current/branding.
/// Anonymous endpoint — keyed to the request's Host header (production)
/// or the X-Tenant-Code header (development / explicit override).
/// </summary>
public record TenantBrandingResponse(
    string TenantId,
    string TenantCode,
    string DisplayName,
    string? LogoUrl,
    string? LogoDocumentId,
    string? LogoWhiteDocumentId,
    string? PrimaryColor,
    string? FaviconUrl);
